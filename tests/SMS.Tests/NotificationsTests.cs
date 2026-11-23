using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Cases;
using SMS.Infrastructure.Exams;
using SMS.Infrastructure.Notifications;
using SMS.Infrastructure.Persistence;
using Xunit;

namespace SMS.Tests;

public class NotificationsTests
{
    static SmsDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<SmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new SmsDbContext(opts);
        CaseTypeSeeder.EnsureSeededAsync(db).GetAwaiter().GetResult();
        return db;
    }
    static AlertsService Alerts(SmsDbContext db)
    {
        var notify = new NotificationService(db);
        return new AlertsService(db, notify, new ExamSchedulingService(db));
    }

    [Fact]
    public async Task CreateIfAbsent_dedupes_unread_for_same_reference()
    {
        using var db = NewDb();
        var svc = new NotificationService(db);
        var a = await svc.CreateIfAbsentAsync("t", null, "CaseOverdue", NotificationSeverity.Error, "Case", 1);
        var b = await svc.CreateIfAbsentAsync("t", null, "CaseOverdue", NotificationSeverity.Error, "Case", 1);
        Assert.NotNull(a);
        Assert.Null(b);                               // deduped
        Assert.Equal(1, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task Generate_creates_an_alert_for_an_overdue_case_and_does_not_duplicate()
    {
        using var db = NewDb();
        // an overdue, open case
        var type = await db.CaseTypes.FirstAsync();
        db.Cases.Add(new Case { CaseNumber = "X-1", CaseTypeId = type.Id, Title = "late", AssignedTo = "jane",
            DueUtc = DateTimeOffset.UtcNow.AddDays(-2), Status = CaseStatus.Open });
        await db.SaveChangesAsync();

        var created1 = await Alerts(db).GenerateAsync();
        var created2 = await Alerts(db).GenerateAsync();   // re-run
        Assert.Equal(1, created1);
        Assert.Equal(0, created2);                          // deduped on second run
        Assert.Contains(db.Notifications, n => n.Category == "CaseOverdue" && n.Recipient == "jane");
    }

    [Fact]
    public async Task Metrics_overview_reflects_students_and_cases()
    {
        using var db = NewDb();
        var prog = new Programme { Id = 1, Code = "P", Name = "P" };
        db.Students.AddRange(
            new Student { StudentNumber = "S1", FirstName = "A", LastName = "A", Programme = prog, Status = StudentStatus.Active },
            new Student { StudentNumber = "S2", FirstName = "B", LastName = "B", Programme = prog, Status = StudentStatus.Active });
        db.Cases.Add(new Case { CaseNumber = "X-2", CaseTypeId = (await db.CaseTypes.FirstAsync()).Id, Title = "open",
            DueUtc = DateTimeOffset.UtcNow.AddDays(-1), Status = CaseStatus.Open });
        await db.SaveChangesAsync();

        var metrics = new MetricsService(db, new ExamSchedulingService(db));
        var overview = await metrics.OverviewAsync();
        var json = System.Text.Json.JsonSerializer.Serialize(overview);
        Assert.Contains("\"total\":2", json);        // 2 students
        Assert.Contains("\"overdue\":1", json);       // 1 overdue case
    }
}
