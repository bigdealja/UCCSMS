using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.StudentServices;

public record ChapelSummary(int StudentId, int Attended, int Total, double AttendanceRate);

/// <summary>Student-services operations: housing, health, sports and chapel attendance.</summary>
public class StudentServicesService
{
    private readonly SmsDbContext _db;
    public StudentServicesService(SmsDbContext db) => _db = db;

    // ---- Housing ----
    public async Task<HousingAssignment> AssignHousingAsync(int studentId, string hall, string room, int? termId, CancellationToken ct = default)
    {
        await EnsureStudent(studentId, ct);
        var h = new HousingAssignment { StudentId = studentId, Hall = hall, Room = room, TermId = termId };
        _db.HousingAssignments.Add(h); await _db.SaveChangesAsync(ct);
        return h;
    }
    public async Task CheckInAsync(int assignmentId, CancellationToken ct = default)
    { var h = await GetHousing(assignmentId, ct); h.Status = HousingStatus.CheckedIn; h.CheckedInUtc = DateTimeOffset.UtcNow; await _db.SaveChangesAsync(ct); }
    public async Task CheckOutAsync(int assignmentId, CancellationToken ct = default)
    { var h = await GetHousing(assignmentId, ct); h.Status = HousingStatus.CheckedOut; h.CheckedOutUtc = DateTimeOffset.UtcNow; await _db.SaveChangesAsync(ct); }

    // ---- Health (confidential) ----
    public async Task<HealthVisit> RecordVisitAsync(int studentId, string reason, string? notes, string? seenBy, CancellationToken ct = default)
    {
        await EnsureStudent(studentId, ct);
        var v = new HealthVisit { StudentId = studentId, Reason = reason, Notes = notes, SeenBy = seenBy };
        _db.HealthVisits.Add(v); await _db.SaveChangesAsync(ct);
        return v;
    }

    // ---- Sports ----
    public async Task<SportsParticipation> AddSportAsync(int studentId, string sport, string? team, string? role, string season, CancellationToken ct = default)
    {
        await EnsureStudent(studentId, ct);
        var s = new SportsParticipation { StudentId = studentId, Sport = sport, Team = team, Role = role, Season = season };
        _db.SportsParticipations.Add(s); await _db.SaveChangesAsync(ct);
        return s;
    }

    // ---- Chapel ----
    public async Task RecordChapelAsync(int studentId, string eventName, DateOnly date, bool present, CancellationToken ct = default)
    {
        await EnsureStudent(studentId, ct);
        var existing = await _db.ChapelAttendances.FirstOrDefaultAsync(a => a.StudentId == studentId && a.EventName == eventName && a.EventDate == date, ct);
        if (existing is null) _db.ChapelAttendances.Add(new ChapelAttendance { StudentId = studentId, EventName = eventName, EventDate = date, Present = present });
        else existing.Present = present;
        await _db.SaveChangesAsync(ct);
    }
    public async Task<ChapelSummary> ChapelSummaryAsync(int studentId, CancellationToken ct = default)
    {
        var records = await _db.ChapelAttendances.AsNoTracking().Where(a => a.StudentId == studentId).ToListAsync(ct);
        int total = records.Count, attended = records.Count(a => a.Present);
        return new ChapelSummary(studentId, attended, total, total == 0 ? 0 : Math.Round(100.0 * attended / total, 1));
    }

    async Task EnsureStudent(int id, CancellationToken ct)
    { if (!await _db.Students.AnyAsync(s => s.Id == id, ct)) throw new InvalidOperationException("Student not found."); }
    async Task<HousingAssignment> GetHousing(int id, CancellationToken ct)
    => await _db.HousingAssignments.FirstOrDefaultAsync(h => h.Id == id, ct) ?? throw new InvalidOperationException("Housing assignment not found.");
}
