using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Examiners;
using SMS.Infrastructure.Persistence;
using Xunit;

namespace SMS.Tests;

public class ExaminerTests
{
    static SmsDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<SmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new SmsDbContext(opts);
    }
    static async Task<(ExaminerService svc, int examinerId)> Setup(SmsDbContext db)
    {
        var svc = new ExaminerService(db);
        var e = await svc.RegisterAsync(new ExternalExaminer { FirstName = "Ext", LastName = "Examiner", Institution = "UWI" });
        return (svc, e.Id);
    }

    [Fact]
    public async Task Cannot_appoint_before_conflict_declaration_is_recorded()
    {
        using var db = NewDb();
        var (svc, id) = await Setup(db);
        var a = await svc.CreateAssignmentAsync(id, null, null);
        await svc.AdvanceAsync(a.Id, AssignmentStage.ConflictCheck, "staff");
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AdvanceAsync(a.Id, AssignmentStage.Appointed, "staff"));
    }

    [Fact]
    public async Task Declared_conflict_blocks_appointment()
    {
        using var db = NewDb();
        var (svc, id) = await Setup(db);
        await svc.DeclareConflictAsync(id, hasConflict: true, "Supervised the candidate");
        var a = await svc.CreateAssignmentAsync(id, null, null);
        await svc.AdvanceAsync(a.Id, AssignmentStage.ConflictCheck, "staff");
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AdvanceAsync(a.Id, AssignmentStage.Appointed, "staff"));
    }

    [Fact]
    public async Task Clean_declaration_allows_appointment_and_updates_status()
    {
        using var db = NewDb();
        var (svc, id) = await Setup(db);
        await svc.DeclareConflictAsync(id, hasConflict: false, null);
        var a = await svc.CreateAssignmentAsync(id, null, null);
        await svc.AdvanceAsync(a.Id, AssignmentStage.ConflictCheck, "staff");
        var appointed = await svc.AdvanceAsync(a.Id, AssignmentStage.Appointed, "staff");
        Assert.Equal(AssignmentStage.Appointed, appointed.Stage);
        Assert.Equal(ExaminerStatus.Appointed, (await db.ExternalExaminers.FindAsync(id))!.Status);
    }

    [Fact]
    public async Task Stage_cannot_move_backwards()
    {
        using var db = NewDb();
        var (svc, id) = await Setup(db);
        var a = await svc.CreateAssignmentAsync(id, null, null);
        await svc.AdvanceAsync(a.Id, AssignmentStage.ConflictCheck, "staff");
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AdvanceAsync(a.Id, AssignmentStage.Nominated, "staff"));
    }

    [Fact]
    public async Task Submitting_a_report_advances_to_ReportReceived()
    {
        using var db = NewDb();
        var (svc, id) = await Setup(db);
        var a = await svc.CreateAssignmentAsync(id, null, null);
        await svc.SubmitReportAsync(a.Id, "All in order", "Minor rubric tweaks", "ext.examiner");
        Assert.Equal(AssignmentStage.ReportReceived, (await db.ExaminerAssignments.FindAsync(a.Id))!.Stage);
    }

    [Fact]
    public async Task Valid_portal_token_resolves_and_expired_does_not()
    {
        using var db = NewDb();
        var (svc, id) = await Setup(db);
        await svc.CreateAssignmentAsync(id, null, null);
        var t = await svc.GeneratePortalTokenAsync(id, 30);
        Assert.NotNull(await svc.ResolvePortalAsync(t.Token));
        Assert.Null(await svc.ResolvePortalAsync("not-a-real-token"));

        // expire it
        var stored = await db.ExaminerPortalTokens.FirstAsync(x => x.Token == t.Token);
        stored.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1); await db.SaveChangesAsync();
        Assert.Null(await svc.ResolvePortalAsync(t.Token));
    }
}
