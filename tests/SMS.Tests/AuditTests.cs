using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Audit;
using SMS.Infrastructure.Persistence;
using Xunit;

namespace SMS.Tests;

public class AuditTests
{
    sealed class FixedUser : ICurrentUserAccessor { public string? UserName { get; init; } = "tester"; }

    static SmsDbContext NewDb(string? user = "tester")
    {
        var opts = new DbContextOptionsBuilder<SmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditSaveChangesInterceptor(new FixedUser { UserName = user }))
            .Options;
        return new SmsDbContext(opts);
    }

    [Fact]
    public async Task Create_and_update_are_audited_with_before_and_after()
    {
        using var db = NewDb();
        var prog = new Programme { Code = "P", Name = "P" };
        db.Programmes.Add(prog);
        var s = new Student { StudentNumber = "S1", FirstName = "Ada", LastName = "L", Programme = prog, Status = StudentStatus.Applicant };
        db.Students.Add(s);
        await db.SaveChangesAsync();

        Assert.Contains(db.AuditEvents, a => a.EntityType == "Student" && a.Action == "Create" && a.Actor == "tester");

        s.Status = StudentStatus.Active;
        await db.SaveChangesAsync();

        var upd = db.AuditEvents.First(a => a.EntityType == "Student" && a.Action == "Update");
        Assert.Contains("Applicant", upd.Before);   // old value captured
        Assert.Contains("Active", upd.After);        // new value captured
    }

    [Fact]
    public async Task Soft_delete_is_audited_as_a_delete()
    {
        using var db = NewDb();
        var prog = new Programme { Code = "P", Name = "P" };
        var s = new Student { StudentNumber = "S1", FirstName = "A", LastName = "B", Programme = prog };
        db.Students.Add(s); await db.SaveChangesAsync();
        db.Students.Remove(s);                        // becomes soft-delete
        await db.SaveChangesAsync();
        Assert.Contains(db.AuditEvents, a => a.EntityType == "Student" && a.Action == "Delete");
    }

    [Fact]
    public async Task Audit_events_do_not_audit_themselves()
    {
        using var db = NewDb();
        db.Programmes.Add(new Programme { Code = "P", Name = "P" });
        await db.SaveChangesAsync();
        Assert.DoesNotContain(db.AuditEvents, a => a.EntityType == "AuditEvent");
    }

    [Fact]
    public async Task Access_logger_records_a_download_event()
    {
        using var db = NewDb("auditor.ann");
        await new AuditLogger(db, new FixedUser { UserName = "auditor.ann" })
            .LogAccessAsync("Download", "Transcript", "42");
        Assert.Contains(db.AuditEvents, a => a.Action == "Download" && a.EntityType == "Transcript" && a.EntityId == "42" && a.Actor == "auditor.ann");
    }
}
