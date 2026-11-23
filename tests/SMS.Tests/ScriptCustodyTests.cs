using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Persistence;
using SMS.Infrastructure.Scripts;
using Xunit;

namespace SMS.Tests;

public class ScriptCustodyTests
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
    static ScriptCustodyService Svc(SmsDbContext db) => new(db);

    [Fact]
    public async Task Creating_a_bundle_logs_a_collected_custody_event()
    {
        using var db = NewDb();
        var b = await Svc(db).CreateBundleAsync(50, "BUN-1", 40, "invig.joe");
        Assert.Equal(ScriptBundleStatus.Collected, b.Status);
        Assert.Contains(db.CustodyEvents, e => e.ScriptBundleId == b.Id && e.Action == CustodyAction.Collected && e.ToCustodian == "invig.joe");
    }

    [Fact]
    public async Task Handover_changes_custodian_and_appends_to_the_chain()
    {
        using var db = NewDb();
        var svc = Svc(db);
        var b = await svc.CreateBundleAsync(50, "BUN-1", 40, "invig.joe");
        await svc.HandoverAsync(b.Id, "exams.office", "invig.joe");
        Assert.Equal("exams.office", (await db.ScriptBundles.FindAsync(b.Id))!.Custodian);
        var chain = await svc.ChainAsync(b.Id);
        Assert.Equal(2, chain.Count);
        Assert.Equal(CustodyAction.Handover, chain[1].Action);
        Assert.Equal("invig.joe", chain[1].FromCustodian);
    }

    [Fact]
    public async Task Full_flow_scan_issue_mark_return_advances_status()
    {
        using var db = NewDb();
        var svc = Svc(db);
        var b = await svc.CreateBundleAsync(50, "BUN-1", 40, "exams.office");
        await svc.RecordScanAsync(b.Id, 40, "COLL-1", "scanner.sue");
        Assert.Equal(ScriptBundleStatus.Scanned, (await db.ScriptBundles.FindAsync(b.Id))!.Status);
        await svc.IssueToMarkerAsync(b.Id, "dr.marker", "exams.office");
        Assert.Equal(ScriptBundleStatus.WithMarker, (await db.ScriptBundles.FindAsync(b.Id))!.Status);
        await svc.UploadMarkedAsync(b.Id, "marked.pdf", null, "dr.marker");
        Assert.Equal(ScriptBundleStatus.Marked, (await db.ScriptBundles.FindAsync(b.Id))!.Status);
    }

    [Fact]
    public async Task Disposal_is_blocked_before_the_retention_date_and_requires_a_witness()
    {
        using var db = NewDb();
        var svc = Svc(db);
        var b = await svc.CreateBundleAsync(50, "BUN-1", 40, "exams.office");

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DisposeAsync(b.Id, "Registrar", "staff")); // no retention set
        await svc.SetRetentionAsync(b.Id, DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1), "staff");
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DisposeAsync(b.Id, "Registrar", "staff")); // before date

        await svc.SetRetentionAsync(b.Id, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), "staff");          // now past
        await svc.DisposeAsync(b.Id, "Registrar", "staff");
        var reloaded = await db.ScriptBundles.FindAsync(b.Id);
        Assert.Equal(ScriptBundleStatus.Disposed, reloaded!.Status);
        Assert.Equal("Registrar", reloaded.DisposalWitness);
    }
}
