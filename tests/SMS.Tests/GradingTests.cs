using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Grading;
using SMS.Infrastructure.Persistence;
using Xunit;

namespace SMS.Tests;

public class GradingTests
{
    static SmsDbContext SeededDb()
    {
        var opts = new DbContextOptionsBuilder<SmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new SmsDbContext(opts);
        GradeSchemeSeeder.EnsureSeededAsync(db).GetAwaiter().GetResult();
        return db;
    }
    static GradeScheme Scheme(SmsDbContext db, ProgrammeLevel level) =>
        db.GradeSchemes.Include(s => s.Bands).First(s => s.Level == level);

    [Theory]
    [InlineData(92, "A", 4.00)]
    [InlineData(72, "B", 3.25)]
    [InlineData(50, "C", 2.16)]
    [InlineData(49, "F", 0.00)]
    public void Bachelors_scale_maps_percent_to_letter_and_points(double pct, string letter, double qp)
    {
        using var db = SeededDb();
        var r = GradingService.Grade(Scheme(db, ProgrammeLevel.Bachelors), pct);
        Assert.Equal(letter, r.Letter);
        Assert.Equal(qp, r.QualityPoints, 2);
    }

    [Fact]
    public void Masters_pass_mark_is_60()
    {
        using var db = SeededDb();
        var s = Scheme(db, ProgrammeLevel.Masters);
        Assert.False(GradingService.CheckPass(s, 80, 80, 58).Passed);   // combined 58 < 60
        Assert.True(GradingService.CheckPass(s, 80, 80, 65).Passed);
    }

    [Fact]
    public void Failing_coursework_fails_regardless_of_combined()
    {
        using var db = SeededDb();
        var s = Scheme(db, ProgrammeLevel.Bachelors);
        var check = GradingService.CheckPass(s, courseworkPct: 45, eosaPct: 90, finalPct: 63);
        Assert.False(check.Passed);
        Assert.Contains("Coursework", check.Reason);
    }

    [Fact]
    public async Task Gpa_excludes_incomplete_and_weights_by_credits()
    {
        using var db = SeededDb();
        var prog = new Programme { Id = 1, Code = "P", Name = "BSc", Level = ProgrammeLevel.Bachelors };
        db.Students.Add(new Student { Id = 1, StudentNumber = "S1", FirstName = "A", LastName = "B", Programme = prog });
        db.Courses.AddRange(
            new Course { Id = 10, Code = "C1", Title = "c1", Programme = prog, Credits = 3 },
            new Course { Id = 11, Code = "C2", Title = "c2", Programme = prog, Credits = 1 });
        // 92 -> A (4.0, 3cr); 50 -> C (2.16, 1cr)  => (4*3 + 2.16*1)/4 = 3.54
        db.Enrollments.AddRange(
            new Enrollment { StudentId = 1, CourseId = 10, TermId = 1, FinalExamMark = 92 },
            new Enrollment { StudentId = 1, CourseId = 11, TermId = 1, FinalExamMark = 50 });
        await db.SaveChangesAsync();

        var gpa = await new GradingService(db).ComputeGpaAsync(1);
        Assert.Equal(3.54, gpa, 2);
    }

    [Fact]
    public void Four_policy_schemes_are_seeded()
    {
        using var db = SeededDb();
        Assert.Equal(4, db.GradeSchemes.Count());
        Assert.Contains(db.GradeSchemes, s => s.Level == ProgrammeLevel.OccupationalDiploma);
    }
}
