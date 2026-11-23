using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.AssessmentOps;
using SMS.Infrastructure.Persistence;
using Xunit;

namespace SMS.Tests;

public class AssessmentOpsTests
{
    static SmsDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<SmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new SmsDbContext(opts);
        var prog = new Programme { Id = 1, Code = "P", Name = "P" };
        db.Courses.Add(new Course { Id = 10, Code = "C", Title = "C", Programme = prog });
        db.Terms.Add(new Term { Id = 7, Name = "Fall", Year = 2026 });
        db.Exams.Add(new Exam { Id = 50, Title = "Final", CourseId = 10, TermId = 7 });
        db.SaveChanges();
        return db;
    }
    static DateOnly In(int days) => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(days);

    [Fact]
    public async Task Milestones_must_be_recorded_in_order()
    {
        using var db = NewDb();
        var svc = new AssessmentOpsService(db);
        var r = await svc.CreateAsync(50, In(30));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RecordMilestoneAsync(r.Id, MilestoneName.Marked)); // scripts not collected
        await svc.RecordMilestoneAsync(r.Id, MilestoneName.ScriptsCollected);
        await svc.RecordMilestoneAsync(r.Id, MilestoneName.Marked);
        var reloaded = await db.SittingRecords.FindAsync(r.Id);
        Assert.NotNull(reloaded!.MarkedUtc);
    }

    [Fact]
    public void Status_is_overdue_when_due_date_passed_and_not_published()
    {
        var r = new SittingRecord { ExamId = 50, DueDate = In(-1), ScriptsCollectedUtc = DateTimeOffset.UtcNow };
        var (stage, status) = AssessmentOpsService.Evaluate(r);
        Assert.Equal("ScriptsCollected", stage);
        Assert.Equal("Overdue", status);
    }

    [Fact]
    public void Exception_blocks_the_sitting()
    {
        var r = new SittingRecord { ExamId = 50, DueDate = In(30), ExceptionNote = "Missing scripts" };
        Assert.Equal("Blocked", AssessmentOpsService.Evaluate(r).Status);
    }

    [Fact]
    public void Published_sitting_is_complete()
    {
        var r = new SittingRecord { ExamId = 50, DueDate = In(-5), PublishedUtc = DateTimeOffset.UtcNow };
        Assert.Equal("Complete", AssessmentOpsService.Evaluate(r).Status);
    }

    [Fact]
    public async Task Needs_attention_lists_overdue_and_blocked_sittings()
    {
        using var db = NewDb();
        db.Exams.Add(new Exam { Id = 51, Title = "F2", CourseId = 10, TermId = 7 });
        await db.SaveChangesAsync();
        var svc = new AssessmentOpsService(db);
        await svc.CreateAsync(50, In(-2));   // overdue
        var ok = await svc.CreateAsync(51, In(30));  // fine
        await svc.RecordMilestoneAsync(ok.Id, MilestoneName.ScriptsCollected);

        var attention = await svc.ListAsync(7, needsAttentionOnly: true);
        Assert.Single(attention);
        Assert.Equal("Overdue", attention[0].Status);
    }
}
