using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Examiners;

/// <summary>External examiner lifecycle: register, conflict declaration (which gates appointment),
/// the 11-stage assignment/package workflow, reports + action plans, and time-limited portal tokens.</summary>
public class ExaminerService
{
    private readonly SmsDbContext _db;
    public ExaminerService(SmsDbContext db) => _db = db;

    public async Task<ExternalExaminer> RegisterAsync(ExternalExaminer e, CancellationToken ct = default)
    { _db.ExternalExaminers.Add(e); await _db.SaveChangesAsync(ct); return e; }

    public async Task DeclareConflictAsync(int examinerId, bool hasConflict, string? details, CancellationToken ct = default)
    {
        var e = await _db.ExternalExaminers.FirstOrDefaultAsync(x => x.Id == examinerId, ct)
                ?? throw new InvalidOperationException("Examiner not found.");
        e.ConflictDeclarationRecorded = true; e.HasConflict = hasConflict; e.ConflictDetails = details;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ExaminerAssignment> CreateAssignmentAsync(int examinerId, int? programmeId, int? termId, CancellationToken ct = default)
    {
        if (!await _db.ExternalExaminers.AnyAsync(x => x.Id == examinerId, ct))
            throw new InvalidOperationException("Examiner not found.");
        var a = new ExaminerAssignment { ExaminerId = examinerId, ProgrammeId = programmeId, TermId = termId };
        _db.ExaminerAssignments.Add(a); await _db.SaveChangesAsync(ct);
        return a;
    }

    /// <summary>Advance an assignment's stage. Appointment (and beyond) is blocked until the conflict
    /// declaration is recorded, and blocked entirely if a conflict exists.</summary>
    public async Task<ExaminerAssignment> AdvanceAsync(int assignmentId, AssignmentStage toStage, string by, CancellationToken ct = default)
    {
        var a = await _db.ExaminerAssignments.Include(x => x.Examiner).FirstOrDefaultAsync(x => x.Id == assignmentId, ct)
                ?? throw new InvalidOperationException("Assignment not found.");
        if (toStage == AssignmentStage.Cancelled) { a.Stage = toStage; await _db.SaveChangesAsync(ct); return a; }
        if (toStage <= a.Stage)
            throw new InvalidOperationException("Stage can only move forward.");

        if (toStage >= AssignmentStage.Appointed)
        {
            if (!a.Examiner.ConflictDeclarationRecorded)
                throw new InvalidOperationException("A conflict-of-interest declaration must be recorded before appointment.");
            if (a.Examiner.HasConflict)
                throw new InvalidOperationException("Examiner has a declared conflict of interest; cannot be appointed.");
        }
        if (toStage == AssignmentStage.PackageDispatched) a.PackageDispatchedUtc = DateTimeOffset.UtcNow;
        if (toStage == AssignmentStage.Appointed) a.Examiner.Status = ExaminerStatus.Appointed;

        a.Stage = toStage;
        await _db.SaveChangesAsync(ct);
        return a;
    }

    public async Task<ExaminerReport> SubmitReportAsync(int assignmentId, string summary, string? recommendations, string by, CancellationToken ct = default)
    {
        var a = await _db.ExaminerAssignments.FirstOrDefaultAsync(x => x.Id == assignmentId, ct)
                ?? throw new InvalidOperationException("Assignment not found.");
        var r = new ExaminerReport { AssignmentId = assignmentId, Summary = summary, Recommendations = recommendations, SubmittedBy = by };
        _db.ExaminerReports.Add(r);
        if (a.Stage < AssignmentStage.ReportReceived) a.Stage = AssignmentStage.ReportReceived;
        await _db.SaveChangesAsync(ct);
        return r;
    }

    public async Task AddActionPlanAsync(int reportId, string plan, string owner, string? corrective, CancellationToken ct = default)
    {
        var r = await _db.ExaminerReports.Include(x => x.Assignment).FirstOrDefaultAsync(x => x.Id == reportId, ct)
                ?? throw new InvalidOperationException("Report not found.");
        r.ActionPlan = plan; r.ActionOwner = owner; r.CorrectiveActions = corrective;
        if (r.Assignment.Stage < AssignmentStage.ActionPlan) r.Assignment.Stage = AssignmentStage.ActionPlan;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ExaminerPortalToken> GeneratePortalTokenAsync(int examinerId, int validDays, CancellationToken ct = default)
    {
        if (!await _db.ExternalExaminers.AnyAsync(x => x.Id == examinerId, ct))
            throw new InvalidOperationException("Examiner not found.");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var t = new ExaminerPortalToken { ExaminerId = examinerId, Token = token, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(validDays) };
        _db.ExaminerPortalTokens.Add(t); await _db.SaveChangesAsync(ct);
        return t;
    }

    /// <summary>Resolve a portal token to the examiner's assignments, or null if invalid/expired.</summary>
    public async Task<(ExternalExaminer Examiner, List<ExaminerAssignment> Assignments)?> ResolvePortalAsync(string token, CancellationToken ct = default)
    {
        var t = await _db.ExaminerPortalTokens.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Token == token && !x.Revoked && x.ExpiresUtc > DateTimeOffset.UtcNow, ct);
        if (t is null) return null;
        var examiner = await _db.ExternalExaminers.AsNoTracking().FirstAsync(x => x.Id == t.ExaminerId, ct);
        var assignments = await _db.ExaminerAssignments.AsNoTracking().Where(x => x.ExaminerId == t.ExaminerId).ToListAsync(ct);
        return (examiner, assignments);
    }
}
