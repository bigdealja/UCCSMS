using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Cases;
using SMS.Infrastructure.Persistence;
using Xunit;

namespace SMS.Tests;

public class CaseTests
{
    static SmsDbContext SeededDb()
    {
        var opts = new DbContextOptionsBuilder<SmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new SmsDbContext(opts);
        CaseTypeSeeder.EnsureSeededAsync(db).GetAwaiter().GetResult();
        return db;
    }

    [Fact]
    public void Fourteen_case_types_are_seeded()
    {
        using var db = SeededDb();
        Assert.Equal(14, db.CaseTypes.Count());
        Assert.Contains(db.CaseTypes, t => t.Code == "APPL" && t.RequiresSecondPersonApproval);
    }

    [Theory]
    [InlineData("2026-12-04", 3, "2026-12-09")]   // Fri + 3 working days -> Wed (skips Sat/Sun)
    [InlineData("2026-12-07", 1, "2026-12-08")]   // Mon + 1 -> Tue
    public void Working_day_due_date_skips_weekends(string from, int days, string expected)
    {
        var start = DateTimeOffset.Parse(from + "T09:00:00Z");
        var due = CaseService.AddWorkingDays(start, days);
        Assert.Equal(DateOnly.Parse(expected), DateOnly.FromDateTime(due.UtcDateTime));
    }

    [Fact]
    public async Task Create_sets_due_date_from_type_turnaround()
    {
        using var db = SeededDb();
        var svc = new CaseService(db);
        var appeal = db.CaseTypes.First(t => t.Code == "APPL");    // 20 working days
        var c = await svc.CreateAsync(appeal.Id, "Grade appeal", null, null, "student.jo", CasePriority.High);
        Assert.True(c.DueUtc > DateTimeOffset.UtcNow.AddDays(25));  // 20 working days ~ 28 calendar days
        Assert.StartsWith("APPL-", c.CaseNumber);
    }

    [Fact]
    public async Task Sensitive_case_needs_a_second_person_to_approve()
    {
        using var db = SeededDb();
        var svc = new CaseService(db);
        var gramd = db.CaseTypes.First(t => t.Code == "GRAMD");   // requires second-person approval
        var c = await svc.CreateAsync(gramd.Id, "Fix grade", null, null, "officer.jane", CasePriority.Normal);
        await svc.AssignAsync(c.Id, "officer.jane", "manager.bob");

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ApproveAsync(c.Id, "officer.jane")); // raiser/assignee
        await svc.ApproveAsync(c.Id, "manager.bob");
        Assert.Equal(CaseStatus.Approved, (await db.Cases.FindAsync(c.Id))!.Status);
    }

    [Fact]
    public async Task Non_sensitive_case_can_be_approved_by_anyone()
    {
        using var db = SeededDb();
        var svc = new CaseService(db);
        var qry = db.CaseTypes.First(t => t.Code == "QRY");   // no second-person requirement
        var c = await svc.CreateAsync(qry.Id, "Question", null, null, "jo", CasePriority.Low);
        await svc.ApproveAsync(c.Id, "jo");
        Assert.Equal(CaseStatus.Approved, (await db.Cases.FindAsync(c.Id))!.Status);
    }

    [Fact]
    public async Task Cancel_records_reason_and_closes_case()
    {
        using var db = SeededDb();
        var svc = new CaseService(db);
        var c = await svc.CreateAsync(db.CaseTypes.First().Id, "x", null, null, "jo", CasePriority.Normal);
        await svc.CancelAsync(c.Id, "duplicate", "jo");
        var reloaded = await db.Cases.FindAsync(c.Id);
        Assert.Equal(CaseStatus.Cancelled, reloaded!.Status);
        Assert.Equal("duplicate", reloaded.CancelReason);
        Assert.NotNull(reloaded.ClosedUtc);
        Assert.Contains(db.CaseActions, a => a.CaseId == c.Id && a.Type == CaseActionType.Cancellation);
    }
}
