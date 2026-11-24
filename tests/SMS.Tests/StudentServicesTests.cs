using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Persistence;
using SMS.Infrastructure.StudentServices;
using Xunit;

namespace SMS.Tests;

public class StudentServicesTests
{
    static SmsDbContext NewDb() =>
        new SmsDbContext(new DbContextOptionsBuilder<SmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    static async Task<int> SeedStudent(SmsDbContext db)
    {
        var s = new Student { StudentNumber = "S9", FirstName = "Test", LastName = "Student", Email = "t@x.edu", Status = StudentStatus.Active };
        db.Students.Add(s); await db.SaveChangesAsync();
        return s.Id;
    }

    [Fact]
    public async Task Housing_assign_then_checkin_and_checkout_tracks_status()
    {
        using var db = NewDb(); var svc = new StudentServicesService(db);
        var sid = await SeedStudent(db);
        var h = await svc.AssignHousingAsync(sid, "North Hall", "204", null);
        Assert.Equal(HousingStatus.Assigned, h.Status);
        await svc.CheckInAsync(h.Id);
        await svc.CheckOutAsync(h.Id);
        var reloaded = await db.HousingAssignments.FindAsync(h.Id);
        Assert.Equal(HousingStatus.CheckedOut, reloaded!.Status);
        Assert.NotNull(reloaded.CheckedInUtc);
        Assert.NotNull(reloaded.CheckedOutUtc);
    }

    [Fact]
    public async Task Chapel_summary_computes_attendance_rate_and_upserts()
    {
        using var db = NewDb(); var svc = new StudentServicesService(db);
        var sid = await SeedStudent(db);
        await svc.RecordChapelAsync(sid, "Week1", new DateOnly(2026, 9, 1), true);
        await svc.RecordChapelAsync(sid, "Week2", new DateOnly(2026, 9, 8), false);
        await svc.RecordChapelAsync(sid, "Week3", new DateOnly(2026, 9, 15), true);
        // upsert: same event/date flips present, does not add a row
        await svc.RecordChapelAsync(sid, "Week2", new DateOnly(2026, 9, 8), true);
        var sum = await svc.ChapelSummaryAsync(sid);
        Assert.Equal(3, sum.Total);
        Assert.Equal(3, sum.Attended);
        Assert.Equal(100.0, sum.AttendanceRate);
    }

    [Fact]
    public async Task Health_visit_is_confidential_by_default()
    {
        using var db = NewDb(); var svc = new StudentServicesService(db);
        var sid = await SeedStudent(db);
        var v = await svc.RecordVisitAsync(sid, "Flu", "rest", "nurse.amy");
        Assert.True(v.Confidential);
    }

    [Fact]
    public async Task Unknown_student_is_rejected()
    {
        using var db = NewDb(); var svc = new StudentServicesService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AddSportAsync(999, "Soccer", null, null, "2026"));
    }
}
