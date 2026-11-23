using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Cases;

/// <summary>Case lifecycle: create (with a working-day due date from the type's turnaround),
/// add notes/actions, change status, approve (second-person where the type requires it),
/// resolve, cancel and link. History is append-only.</summary>
public class CaseService
{
    private readonly SmsDbContext _db;
    public CaseService(SmsDbContext db) => _db = db;

    /// <summary>Add N working days (skips Saturday/Sunday). Holidays are a later calendar concern.</summary>
    public static DateTimeOffset AddWorkingDays(DateTimeOffset from, int days)
    {
        var d = from;
        int added = 0;
        while (added < days)
        {
            d = d.AddDays(1);
            if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday) added++;
        }
        return d;
    }

    public async Task<Case> CreateAsync(int caseTypeId, string title, string? description, int? studentId,
        string raisedBy, CasePriority priority, CancellationToken ct = default)
    {
        var type = await _db.CaseTypes.FirstOrDefaultAsync(t => t.Id == caseTypeId, ct)
                   ?? throw new InvalidOperationException($"Case type {caseTypeId} not found.");
        var c = new Case
        {
            CaseTypeId = caseTypeId, Title = title, Description = description, StudentId = studentId,
            RaisedBy = raisedBy, Priority = priority,
            CaseNumber = $"{type.Code}-{DateTime.UtcNow:yyyy}-{Guid.NewGuid().ToString("N")[..5].ToUpper()}",
            DueUtc = AddWorkingDays(DateTimeOffset.UtcNow, type.TurnaroundWorkingDays)
        };
        _db.Cases.Add(c);
        await _db.SaveChangesAsync(ct);
        return c;
    }

    public async Task AddNoteAsync(int caseId, string text, string by, CancellationToken ct = default)
        => await AddActionAsync(caseId, CaseActionType.Note, text, by, null, ct);

    public async Task AssignAsync(int caseId, string assignee, string by, CancellationToken ct = default)
    {
        var c = await Get(caseId, ct);
        c.AssignedTo = assignee;
        if (c.Status == CaseStatus.Open) c.Status = CaseStatus.InProgress;
        await AddActionAsync(caseId, CaseActionType.Assignment, $"Assigned to {assignee}", by, null, ct);
    }

    /// <summary>Approve a case. For types that require it, the approver must differ from the raiser
    /// and the assignee (nobody approves their own work).</summary>
    public async Task ApproveAsync(int caseId, string approver, CancellationToken ct = default)
    {
        var c = await _db.Cases.Include(x => x.CaseType).FirstOrDefaultAsync(x => x.Id == caseId, ct)
                ?? throw new InvalidOperationException("Case not found.");
        if (c.CaseType.RequiresSecondPersonApproval)
        {
            if (string.Equals(approver, c.RaisedBy, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(approver, c.AssignedTo, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("This case type needs a second person to approve (not the raiser or assignee).");
        }
        c.Status = CaseStatus.Approved;
        c.ApprovedBy = approver;
        await AddActionAsync(caseId, CaseActionType.Approval, "Approved", approver, null, ct);
    }

    public async Task ResolveAsync(int caseId, string resolution, string by, CancellationToken ct = default)
    {
        var c = await Get(caseId, ct);
        c.Status = CaseStatus.Resolved; c.Resolution = resolution; c.ClosedUtc = DateTimeOffset.UtcNow;
        await AddActionAsync(caseId, CaseActionType.StatusChange, $"Resolved: {resolution}", by, null, ct);
    }

    public async Task CancelAsync(int caseId, string reason, string by, CancellationToken ct = default)
    {
        var c = await Get(caseId, ct);
        c.Status = CaseStatus.Cancelled; c.CancelReason = reason; c.ClosedUtc = DateTimeOffset.UtcNow;
        await AddActionAsync(caseId, CaseActionType.Cancellation, $"Cancelled: {reason}", by, null, ct);
    }

    public async Task LinkAsync(int caseId, int linkedCaseId, string by, CancellationToken ct = default)
        => await AddActionAsync(caseId, CaseActionType.Link, $"Linked to case {linkedCaseId}", by, linkedCaseId, ct);

    async Task<Case> Get(int id, CancellationToken ct)
        => await _db.Cases.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new InvalidOperationException("Case not found.");

    async Task AddActionAsync(int caseId, CaseActionType type, string text, string by, int? linked, CancellationToken ct)
    {
        _db.CaseActions.Add(new CaseAction { CaseId = caseId, Type = type, Text = text, By = by, LinkedCaseId = linked });
        await _db.SaveChangesAsync(ct);
    }
}
