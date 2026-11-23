using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Grading;
using SMS.Infrastructure.Persistence;
using Xunit;

namespace SMS.Tests;

public class GradeGovernanceTests
{
    static SmsDbContext NewDb(out Enrollment enrollment)
    {
        var opts = new DbContextOptionsBuilder<SmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new SmsDbContext(opts);
        var prog = new Programme { Id = 1, Code = "P", Name = "P" };
        var course = new Course { Id = 10, Code = "C", Title = "C", Programme = prog };
        db.Courses.Add(course);
        db.Terms.Add(new Term { Id = 7, Name = "Fall", Year = 2026 });
        db.Students.Add(new Student { Id = 1, StudentNumber = "S1", FirstName = "A", LastName = "A", Programme = prog });
        enrollment = new Enrollment { Id = 100, StudentId = 1, CourseId = 10, TermId = 7, Status = EnrollmentStatus.Registered,
            LetterGrade = "F", FinalExamMark = 45 };
        db.Enrollments.Add(enrollment);
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Propose_snapshots_original_grade_without_changing_it()
    {
        using var db = NewDb(out _);
        var svc = new GradeGovernanceService(db);
        var req = await svc.ProposeAsync(100, "C", 55, GradeChangeReasonCode.RemarkOutcome, "re-mark", "lecturer.sam");
        Assert.Equal("F", req.OriginalLetterGrade);          // original captured
        Assert.Equal("C", req.ProposedLetterGrade);
        var e = await db.Enrollments.FindAsync(100);
        Assert.Equal("F", e!.LetterGrade);                   // enrollment NOT changed yet
    }

    [Fact]
    public async Task Approve_requires_second_person_then_applies_change()
    {
        using var db = NewDb(out _);
        var svc = new GradeGovernanceService(db);
        var req = await svc.ProposeAsync(100, "C", 55, GradeChangeReasonCode.RemarkOutcome, "re-mark", "lecturer.sam");

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ApproveAndApplyAsync(req.Id, "lecturer.sam")); // proposer
        var applied = await svc.ApproveAndApplyAsync(req.Id, "dean.pat");

        Assert.Equal(GradeChangeStatus.Applied, applied.Status);
        var e = await db.Enrollments.FindAsync(100);
        Assert.Equal("C", e!.LetterGrade);                   // now applied
        Assert.Equal("GradeChange", e.GradeSource);
        Assert.Equal("F", applied.OriginalLetterGrade);      // original preserved on the record
    }

    [Fact]
    public async Task Withhold_sets_flag_and_links_case()
    {
        using var db = NewDb(out _);
        var svc = new GradeGovernanceService(db);
        await svc.WithholdAsync(100, caseId: 42);
        var e = await db.Enrollments.FindAsync(100);
        Assert.True(e!.IsWithheld);
        Assert.Equal(42, e.WithheldCaseId);
    }

    [Fact]
    public async Task Pre_publication_flags_ungraded_withheld_and_pending()
    {
        using var db = NewDb(out var enr);
        var svc = new GradeGovernanceService(db);
        // add an ungraded enrolment
        db.Enrollments.Add(new Enrollment { Id = 101, StudentId = 1, CourseId = 10, TermId = 7, Status = EnrollmentStatus.Registered });
        await db.SaveChangesAsync();
        await svc.WithholdAsync(100, 42);                                      // withheld
        await svc.ProposeAsync(100, "C", 55, GradeChangeReasonCode.Other, null, "sam"); // pending change

        var report = await svc.PrePublicationCheckAsync(7);
        Assert.False(report.ReadyToPublish);
        Assert.Contains(report.Issues, i => i.Kind == "Ungraded" && i.EnrollmentId == 101);
        Assert.Contains(report.Issues, i => i.Kind == "Withheld" && i.EnrollmentId == 100);
        Assert.Contains(report.Issues, i => i.Kind == "PendingChange" && i.EnrollmentId == 100);
    }
}
