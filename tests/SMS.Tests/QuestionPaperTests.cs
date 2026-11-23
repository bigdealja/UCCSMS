using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Persistence;
using SMS.Infrastructure.QuestionPapers;
using Xunit;

namespace SMS.Tests;

public class QuestionPaperTests
{
    static SmsDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<SmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new SmsDbContext(opts);
        var prog = new Programme { Id = 1, Code = "P", Name = "P" };
        db.Courses.Add(new Course { Id = 10, Code = "C", Title = "C", Programme = prog });
        db.Terms.Add(new Term { Id = 7, Name = "Fall", Year = 2026 });
        db.SaveChanges();
        return db;
    }
    static QuestionPaperService Svc(SmsDbContext db) => new(db);
    static readonly DateOnly DL = new(2026, 12, 1);

    async Task<(QuestionPaperService svc, int id)> Registered(SmsDbContext db, bool extReview)
    {
        var svc = Svc(db);
        var p = await svc.RegisterAsync(10, 7, "CS101 Final", DL, "Dr Smith", extReview);
        await svc.SubmitVersionAsync(p.Id, "paper-v1.pdf", null, "dr.smith", null);
        return (svc, p.Id);
    }

    [Fact]
    public async Task Submitting_a_version_moves_from_Expected_to_Submitted()
    {
        using var db = NewDb();
        var (_, id) = await Registered(db, false);
        Assert.Equal(PaperStatus.Submitted, (await db.QuestionPapers.FindAsync(id))!.Status);
        Assert.Equal(1, (await db.QuestionPapers.FindAsync(id))!.CurrentVersion);
    }

    [Fact]
    public async Task External_review_is_required_before_approval_when_flagged()
    {
        using var db = NewDb();
        var (svc, id) = await Registered(db, extReview: true);
        await svc.AdvanceAsync(id, PaperStatus.UnderVetting);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AdvanceAsync(id, PaperStatus.Approved));
        await svc.AdvanceAsync(id, PaperStatus.ExternalReview);
        var approved = await svc.AdvanceAsync(id, PaperStatus.Approved);
        Assert.Equal(PaperStatus.Approved, approved.Status);
    }

    [Fact]
    public async Task Cannot_print_before_release()
    {
        using var db = NewDb();
        var (svc, id) = await Registered(db, false);
        await svc.AdvanceAsync(id, PaperStatus.UnderVetting);
        await svc.AdvanceAsync(id, PaperStatus.Approved);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RecordPrintRunAsync(id, 100, 5, "Vault", "SR-1"));
    }

    [Fact]
    public async Task Destruction_requires_a_recorded_return_and_a_witness()
    {
        using var db = NewDb();
        var (svc, id) = await Registered(db, false);
        await svc.AdvanceAsync(id, PaperStatus.UnderVetting);
        await svc.AdvanceAsync(id, PaperStatus.Approved);
        await svc.AdvanceAsync(id, PaperStatus.Released);
        var run = await svc.RecordPrintRunAsync(id, 100, 5, "Vault", "SR-1");
        await svc.DispatchAsync(run.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DestroyAsync(run.Id, "Registrar")); // no return yet
        await svc.RecordReturnAsync(run.Id, 7);
        await svc.DestroyAsync(run.Id, "Registrar");

        var p = await db.QuestionPapers.FindAsync(id);
        Assert.Equal(PaperStatus.Destroyed, p!.Status);
        var r = await db.PrintRuns.FindAsync(run.Id);
        Assert.Equal("Registrar", r!.DestructionWitness);
    }

    [Fact]
    public async Task Emergency_withdrawal_works_from_any_stage()
    {
        using var db = NewDb();
        var (svc, id) = await Registered(db, false);
        await svc.WithdrawAsync(id, "Leak suspected");
        var p = await db.QuestionPapers.FindAsync(id);
        Assert.Equal(PaperStatus.Withdrawn, p!.Status);
        Assert.Equal("Leak suspected", p.WithdrawnReason);
    }
}
