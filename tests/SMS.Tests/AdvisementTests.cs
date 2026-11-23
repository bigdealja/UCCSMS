using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Advisement;
using SMS.Infrastructure.Grading;
using SMS.Infrastructure.Persistence;
using Xunit;

namespace SMS.Tests;

public class AdvisementTests
{
    static SmsDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<SmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new SmsDbContext(opts);
        GradeSchemeSeeder.EnsureSeededAsync(db).GetAwaiter().GetResult();
        var prog = new Programme { Id = 1, Code = "BSC", Name = "BSc CS", Level = ProgrammeLevel.Bachelors, CreditsRequired = 9 };
        db.Programmes.Add(prog);
        db.Courses.AddRange(
            new Course { Id = 10, Code = "CS101", Title = "Intro", Programme = prog, Credits = 3 },
            new Course { Id = 11, Code = "CS102", Title = "Data", Programme = prog, Credits = 3 },
            new Course { Id = 12, Code = "CS103", Title = "Algos", Programme = prog, Credits = 3 });
        db.Terms.Add(new Term { Id = 7, Name = "Fall", Year = 2026 });
        db.Students.Add(new Student { Id = 1, StudentNumber = "S1", FirstName = "Ada", LastName = "L", Programme = prog });
        db.SaveChanges();
        return db;
    }
    static AdvisementService Svc(SmsDbContext db) => new(db, new GradingService(db));

    [Fact]
    public async Task Progress_counts_earned_credits_and_outstanding_courses()
    {
        using var db = NewDb();
        // CS101 passed (A, 92), CS102 failed (F, 30), CS103 not attempted
        db.Enrollments.AddRange(
            new Enrollment { StudentId = 1, CourseId = 10, TermId = 7, LetterGrade = "A", FinalExamMark = 92 },
            new Enrollment { StudentId = 1, CourseId = 11, TermId = 7, LetterGrade = "F", FinalExamMark = 30 });
        await db.SaveChangesAsync();

        var p = await Svc(db).GetProgressAsync(1);
        Assert.Equal(3, p.CreditsEarned);               // only CS101
        Assert.Single(p.Completed);
        Assert.Equal(2, p.Outstanding.Count);           // CS102 (failed) + CS103 (not attempted)
        Assert.Single(p.Failed);                        // CS102
    }

    [Fact]
    public async Task Not_eligible_to_graduate_with_outstanding_courses()
    {
        using var db = NewDb();
        db.Enrollments.Add(new Enrollment { StudentId = 1, CourseId = 10, TermId = 7, LetterGrade = "A", FinalExamMark = 92 });
        await db.SaveChangesAsync();
        var v = await Svc(db).GraduationCheckAsync(1);
        Assert.False(v.Eligible);
        Assert.Contains(v.Reasons, r => r.Contains("outstanding"));
        Assert.Contains(v.Reasons, r => r.Contains("credits short"));
    }

    [Fact]
    public async Task Eligible_when_all_courses_passed_and_gpa_sufficient()
    {
        using var db = NewDb();
        db.Enrollments.AddRange(
            new Enrollment { StudentId = 1, CourseId = 10, TermId = 7, LetterGrade = "A", FinalExamMark = 92 },
            new Enrollment { StudentId = 1, CourseId = 11, TermId = 7, LetterGrade = "B", FinalExamMark = 70 },
            new Enrollment { StudentId = 1, CourseId = 12, TermId = 7, LetterGrade = "C", FinalExamMark = 52 });
        await db.SaveChangesAsync();
        var v = await Svc(db).GraduationCheckAsync(1);
        Assert.True(v.Eligible);
        Assert.Empty(v.Reasons);
        Assert.Equal(9, v.CreditsEarned);
    }
}
