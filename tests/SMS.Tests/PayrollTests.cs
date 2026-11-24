using Microsoft.EntityFrameworkCore;
using SMS.Application.Hr;
using SMS.Domain.Enums;
using SMS.Infrastructure.Hr;
using SMS.Infrastructure.Persistence;
using Xunit;

namespace SMS.Tests;

public class PayrollTests
{
    class FakeHr : IHrProvider
    {
        public List<HrExportRequest> Exported = new();
        public string Name => "FakeHR";
        public bool IsExternal => true;
        public Task<bool> HealthAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<HrExportResult> ExportAsync(HrExportRequest r, CancellationToken ct = default)
        { Exported.Add(r); return Task.FromResult(HrExportResult.Ok("HR-" + (Exported.Count))); }
    }

    static SmsDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<SmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new SmsDbContext(opts);
    }
    static readonly DateOnly D = new(2026, 11, 1);

    [Fact]
    public async Task Effective_rate_is_the_latest_on_or_before_the_date()
    {
        using var db = NewDb();
        var svc = new PayrollService(db, new NullHrProvider());
        await svc.AddRateAsync(WorkType.Marking, 100m, "script", new DateOnly(2025, 1, 1));
        await svc.AddRateAsync(WorkType.Marking, 150m, "script", new DateOnly(2026, 8, 1));
        var rate = await svc.GetEffectiveRateAsync(WorkType.Marking, D);
        Assert.Equal(150m, rate!.RatePerUnit);
    }

    [Fact]
    public async Task Claim_amount_is_units_times_rate()
    {
        using var db = NewDb();
        var svc = new PayrollService(db, new NullHrProvider());
        await svc.AddRateAsync(WorkType.Marking, 150m, "script", new DateOnly(2026, 8, 1));
        var c = await svc.CreateClaimAsync("Dr Smith", null, WorkType.Marking, 20, D, "40 scripts marked");
        Assert.Equal(3000m, c.Amount);       // 20 * 150
        Assert.Equal(150m, c.RatePerUnit);
    }

    [Fact]
    public async Task Claim_without_a_rate_is_rejected()
    {
        using var db = NewDb();
        var svc = new PayrollService(db, new NullHrProvider());
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateClaimAsync("X", null, WorkType.Invigilation, 3, D, null));
    }

    [Fact]
    public async Task Approval_requires_a_second_person_then_export_sets_external_ref()
    {
        using var db = NewDb();
        var fake = new FakeHr();
        var svc = new PayrollService(db, fake);
        await svc.AddRateAsync(WorkType.Invigilation, 50m, "hour", new DateOnly(2026, 1, 1));
        var c = await svc.CreateClaimAsync("Joe", null, WorkType.Invigilation, 4, D, null);
        await svc.SubmitAsync(c.Id, "clerk.ann");

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ApproveAsync(c.Id, "clerk.ann")); // submitter
        await svc.ApproveAsync(c.Id, "manager.bob");

        var exported = await svc.ExportAsync(c.Id);
        Assert.Equal(ClaimStatus.Exported, exported.Status);
        Assert.Equal("HR-1", exported.ExternalRef);
        Assert.Single(fake.Exported);
        Assert.Equal(200m, fake.Exported[0].Amount);  // 4 * 50
    }

    [Fact]
    public async Task Only_approved_claims_can_be_exported()
    {
        using var db = NewDb();
        var svc = new PayrollService(db, new FakeHr());
        await svc.AddRateAsync(WorkType.Marking, 100m, "script", new DateOnly(2026, 1, 1));
        var c = await svc.CreateClaimAsync("X", null, WorkType.Marking, 1, D, null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ExportAsync(c.Id)); // still Draft
    }
}
