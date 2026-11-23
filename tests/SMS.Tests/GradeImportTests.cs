using System.Text;
using Microsoft.EntityFrameworkCore;
using SMS.Application.GradeImport;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.GradeImport;
using SMS.Infrastructure.Persistence;
using Xunit;

namespace SMS.Tests;

public class GradeImportTests
{
    static SmsDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<SmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new SmsDbContext(opts);
        var prog = new Programme { Id = 1, Code = "P", Name = "Prog" };
        var course = new Course { Id = 10, Code = "CS101", Title = "Intro", Programme = prog };
        db.Courses.Add(course);
        db.Students.AddRange(
            new Student { Id = 1, StudentNumber = "S0001", FirstName = "Ada", LastName = "L", Programme = prog },
            new Student { Id = 2, StudentNumber = "S0002", FirstName = "Alan", LastName = "T", Programme = prog });
        db.SaveChanges();
        return db;
    }

    static ParsedSheet Csv(string text) =>
        new SpreadsheetGradeFileParser().Parse(new MemoryStream(Encoding.UTF8.GetBytes(text)), "x.csv");

    [Fact]
    public void Csv_parser_reads_headers_and_quoted_fields()
    {
        var sheet = Csv("StudentNo,Grade\nS0001,A\n\"S0002\",\"B+\"\n");
        Assert.Equal(new[] { "StudentNo", "Grade" }, sheet.Headers);
        Assert.Equal(2, sheet.Rows.Count);
        Assert.Equal("B+", sheet.Rows[1][1]);
    }

    [Fact]
    public async Task Preview_flags_unknown_student_and_counts_ok()
    {
        using var db = NewDb();
        var svc = new GradeImportService(db);
        var sheet = Csv("StudentNo,Grade\nS0001,A\nS9999,C\n");
        var map = new GradeImportMapping("StudentNo", "Grade", null, null);

        var preview = await svc.PreviewAsync(1, 10, sheet, map);

        Assert.Equal(1, preview.OkCount);                 // S0001 resolves
        Assert.Equal(1, preview.ErrorCount);              // S9999 unknown
        Assert.Contains(preview.Rows, r => r.StudentNumber == "S9999" && r.Status == RowStatus.Error);
    }

    [Fact]
    public async Task Commit_writes_one_undoable_batch_and_skips_errors()
    {
        using var db = NewDb();
        var svc = new GradeImportService(db);
        var sheet = Csv("StudentNo,Grade\nS0001,A\nS0002,B\nS9999,C\n");
        var map = new GradeImportMapping("StudentNo", "Grade", null, null);

        var preview = await svc.PreviewAsync(1, 10, sheet, map);
        var result = await svc.CommitAsync(1, 10, preview, "tester");

        Assert.Equal(2, result.Applied);                  // two valid rows
        Assert.Equal(1, result.Skipped);                  // the unknown student
        Assert.NotEqual(Guid.Empty, result.BatchId);

        var grades = await db.Enrollments.ToListAsync();
        Assert.Equal(2, grades.Count);
        Assert.All(grades, g => Assert.Equal(result.BatchId, g.ImportBatchId));
        Assert.All(grades, g => Assert.Equal("Import", g.GradeSource));
    }

    [Fact]
    public async Task Reimport_overwrites_existing_grade_and_warns()
    {
        using var db = NewDb();
        var svc = new GradeImportService(db);
        var map = new GradeImportMapping("StudentNo", "Grade", null, null);

        await svc.CommitAsync(1, 10, await svc.PreviewAsync(1, 10, Csv("StudentNo,Grade\nS0001,A\n"), map), "t");
        var preview2 = await svc.PreviewAsync(1, 10, Csv("StudentNo,Grade\nS0001,B\n"), map);

        Assert.Equal(1, preview2.WarningCount);           // would overwrite
        Assert.Contains(preview2.Rows, r => !r.IsNewGrade);
    }
}
