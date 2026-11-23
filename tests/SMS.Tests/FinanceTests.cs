using Microsoft.EntityFrameworkCore;
using SMS.Application.Accounting;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Accounting;
using SMS.Infrastructure.Finance;
using SMS.Infrastructure.Persistence;
using Xunit;

namespace SMS.Tests;

/// <summary>Stands in for any external accounting system (Sage/QuickBooks/Xero) so we can prove
/// the finance core is provider-agnostic without a real ERP.</summary>
class FakeAccountingProvider : IAccountingProvider
{
    public List<AccountingPostRequest> Posted = new();
    public string Name => "Fake";
    public bool IsExternal => true;
    public Task<bool> HealthAsync(CancellationToken ct = default) => Task.FromResult(true);
    public Task<AccountingPostResult> PostAsync(AccountingPostRequest r, CancellationToken ct = default)
    { Posted.Add(r); return Task.FromResult(AccountingPostResult.Ok("EXT-" + (Posted.Count))); }
}

public class FinanceTests
{
    static SmsDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<SmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new SmsDbContext(opts);
        var prog = new Programme { Id = 1, Code = "P", Name = "Prog" };
        db.Students.Add(new Student { Id = 1, StudentNumber = "S0001", FirstName = "Ada", LastName = "L", Programme = prog });
        db.FeeTypes.Add(new FeeType { Id = 1, Code = "TUIT", Name = "Tuition", DefaultAmount = 1000m, DistributionCode = "4020-01-01" });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Charge_then_payment_computes_balance()
    {
        using var db = NewDb();
        var svc = new FinanceService(db, new NullAccountingProvider());
        await svc.PostChargeAsync(1, null, 1, 1000m, "Tuition");
        await svc.RecordPaymentAsync(1, 400m, PaymentMethod.Card, "RCPT-1", null);

        Assert.Equal(600m, await svc.GetBalanceAsync(1));
        var stmt = await svc.GetStatementAsync(1);
        Assert.Equal(600m, stmt.Balance);
        Assert.Equal(1000m, stmt.Lines[0].RunningBalance);
        Assert.Equal(600m, stmt.Lines[1].RunningBalance);
    }

    [Fact]
    public async Task None_provider_does_not_post_externally()
    {
        using var db = NewDb();
        var svc = new FinanceService(db, new NullAccountingProvider());
        await svc.PostChargeAsync(1, null, 1, 500m, "Lab fee");
        var tx = await db.FinancialTransactions.FirstAsync();
        Assert.Equal(AccountingSyncStatus.NotApplicable, tx.AccountingStatus);
        Assert.Null(tx.AccountingRef);
        Assert.Equal("None", tx.AccountingProvider);
    }

    [Fact]
    public async Task Configured_provider_receives_post_and_records_external_ref()
    {
        using var db = NewDb();
        var fake = new FakeAccountingProvider();
        var svc = new FinanceService(db, fake);
        await svc.PostChargeAsync(1, null, 1, 1000m, "Tuition");

        Assert.Single(fake.Posted);
        Assert.Equal("S0001", fake.Posted[0].StudentNumber);
        Assert.Equal("4020-01-01", fake.Posted[0].DistributionCode);   // fee's GL code flows through
        var tx = await db.FinancialTransactions.FirstAsync();
        Assert.Equal(AccountingSyncStatus.Posted, tx.AccountingStatus);
        Assert.Equal("EXT-1", tx.AccountingRef);
        Assert.Equal("Fake", tx.AccountingProvider);
    }

    [Fact]
    public async Task Negative_or_zero_charge_is_rejected()
    {
        using var db = NewDb();
        var svc = new FinanceService(db, new NullAccountingProvider());
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.PostChargeAsync(1, null, 1, 0m, "bad"));
    }
}
