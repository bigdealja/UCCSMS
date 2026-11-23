using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Exams;
using SMS.Infrastructure.Persistence;
using Xunit;

namespace SMS.Tests;

public class ExamTests
{
    static SmsDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<SmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new SmsDbContext(opts);
        var prog = new Programme { Id = 1, Code = "P", Name = "Prog" };
        var course = new Course { Id = 10, Code = "CS101", Title = "Intro", Programme = prog };
        var term = new Term { Id = 7, Name = "Fall", Year = 2026 };
        db.AddRange(course, term);
        db.Students.AddRange(
            new Student { Id = 1, StudentNumber = "S0001", FirstName = "Ada", LastName = "L", Programme = prog },
            new Student { Id = 2, StudentNumber = "S0002", FirstName = "Al", LastName = "T", Programme = prog });
        db.Enrollments.AddRange(
            new Enrollment { StudentId = 1, CourseId = 10, TermId = 7, Status = EnrollmentStatus.Registered },
            new Enrollment { StudentId = 2, CourseId = 10, TermId = 7, Status = EnrollmentStatus.Registered });
        db.Exams.Add(new Exam { Id = 100, Title = "Midterm", CourseId = 10, TermId = 7, Status = ExamStatus.Scheduled });
        db.Questions.AddRange(
            new Question { Id = 1, CourseId = 10, Type = QuestionType.MultipleChoice, Text = "Q1", DefaultMarks = 5 },
            new Question { Id = 2, CourseId = 10, Type = QuestionType.ShortAnswer, Text = "Q2", DefaultMarks = 10 });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Adding_questions_sums_total_marks()
    {
        using var db = NewDb();
        var svc = new ExamService(db);
        await svc.AddQuestionAsync(100, 1, null, default);   // 5
        await svc.AddQuestionAsync(100, 2, 15, default);     // override to 15
        var exam = await db.Exams.FindAsync(100);
        Assert.Equal(20, exam!.TotalMarks);
    }

    [Fact]
    public async Task Adding_same_question_twice_is_rejected()
    {
        using var db = NewDb();
        var svc = new ExamService(db);
        await svc.AddQuestionAsync(100, 1, null, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AddQuestionAsync(100, 1, null, default));
    }

    [Fact]
    public async Task Posting_results_writes_grades_as_one_undoable_batch()
    {
        using var db = NewDb();
        var svc = new ExamService(db);
        await svc.RecordResultAsync(100, 1, 88, default);
        await svc.RecordResultAsync(100, 2, 74, default);

        var outcome = await svc.PostResultsToGradebookAsync(100, default);

        Assert.Equal(2, outcome.Posted);
        var grades = await db.Enrollments.ToListAsync();
        Assert.All(grades, g => Assert.Equal("Exam", g.GradeSource));
        Assert.All(grades, g => Assert.Equal(outcome.BatchId, g.ImportBatchId));
        Assert.Equal(88, (await db.Enrollments.FirstAsync(e => e.StudentId == 1)).FinalExamMark);
        Assert.Equal(ExamStatus.Completed, (await db.Exams.FindAsync(100))!.Status);
    }
}
