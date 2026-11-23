using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Grading;
using SMS.Infrastructure.Persistence;
using Xunit;

namespace SMS.Tests;

public class GradebookTests
{
    static SmsDbContext NewDb(out int studentId)
    {
        var opts = new DbContextOptionsBuilder<SmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new SmsDbContext(opts);
        GradeSchemeSeeder.EnsureSeededAsync(db).GetAwaiter().GetResult();
        var prog = new Programme { Id = 1, Code = "P", Name = "BSc", Level = ProgrammeLevel.Bachelors };
        db.Courses.Add(new Course { Id = 10, Code = "C", Title = "C", Programme = prog, Credits = 3 });
        db.Terms.Add(new Term { Id = 7, Name = "Fall", Year = 2026 });
        var s = new Student { Id = 1, StudentNumber = "S1", FirstName = "Ada", LastName = "L", Programme = prog };
        db.Students.Add(s);
        db.Enrollments.Add(new Enrollment { StudentId = 1, CourseId = 10, TermId = 7, Status = EnrollmentStatus.Registered });
        db.SaveChanges();
        studentId = 1;
        return db;
    }
    static GradebookService Svc(SmsDbContext db) => new(db, new GradingService(db));

    [Fact]
    public async Task Weighted_components_compute_final_percent_and_letter()
    {
        using var db = NewDb(out var sid);
        var svc = Svc(db);
        // Coursework 60 (two items of 30), EOSA 40. All out of 100.
        var a1 = await svc.AddComponentAsync(10, 7, "Assignment 1", GradeCategory.Coursework, 30, 100);
        var a2 = await svc.AddComponentAsync(10, 7, "Mid-semester", GradeCategory.Coursework, 30, 100);
        var fe = await svc.AddComponentAsync(10, 7, "Final Exam", GradeCategory.EndOfSemester, 40, 100);
        await svc.EnterMarkAsync(a1.Id, sid, 80, "lect");
        await svc.EnterMarkAsync(a2.Id, sid, 70, "lect");
        await svc.EnterMarkAsync(fe.Id, sid, 60, "lect");

        var g = await svc.ComputeAsync(10, 7, sid);
        // coursework% = (80*30 + 70*30)/60 = 75 ; final% = (80*30+70*30+60*40)/100 = 69
        Assert.Equal(75, g.CourseworkPct, 1);
        Assert.Equal(69, g.FinalPct, 1);
        Assert.Equal("B", g.Letter);      // Bachelors 65-74 -> B
        Assert.True(g.Passed);
    }

    [Fact]
    public async Task Failing_coursework_fails_the_course_even_with_a_high_final()
    {
        using var db = NewDb(out var sid);
        var svc = Svc(db);
        var cw = await svc.AddComponentAsync(10, 7, "Coursework", GradeCategory.Coursework, 60, 100);
        var fe = await svc.AddComponentAsync(10, 7, "Final", GradeCategory.EndOfSemester, 40, 100);
        await svc.EnterMarkAsync(cw.Id, sid, 40, "lect");   // 40% coursework -> below 50
        await svc.EnterMarkAsync(fe.Id, sid, 95, "lect");

        var g = await svc.ComputeAsync(10, 7, sid);
        Assert.False(g.Passed);
        Assert.Contains("Coursework", g.Reason);
    }

    [Fact]
    public async Task Mark_above_max_is_rejected()
    {
        using var db = NewDb(out var sid);
        var svc = Svc(db);
        var c = await svc.AddComponentAsync(10, 7, "Quiz", GradeCategory.Coursework, 100, 20);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.EnterMarkAsync(c.Id, sid, 25, "lect"));
    }

    [Fact]
    public async Task Posting_writes_the_computed_grade_to_the_enrolment_as_a_batch()
    {
        using var db = NewDb(out var sid);
        var svc = Svc(db);
        var cw = await svc.AddComponentAsync(10, 7, "Coursework", GradeCategory.Coursework, 60, 100);
        var fe = await svc.AddComponentAsync(10, 7, "Final", GradeCategory.EndOfSemester, 40, 100);
        await svc.EnterMarkAsync(cw.Id, sid, 90, "lect");
        await svc.EnterMarkAsync(fe.Id, sid, 90, "lect");

        var result = await svc.PostAsync(10, 7);
        Assert.Equal(1, result.Written);
        var e = await db.Enrollments.FirstAsync(x => x.StudentId == sid);
        Assert.Equal("A", e.LetterGrade);          // 90 -> A
        Assert.Equal("Gradebook", e.GradeSource);
        Assert.Equal(result.BatchId, e.ImportBatchId);  // undoable like imports/Moodle/exam
    }
}
