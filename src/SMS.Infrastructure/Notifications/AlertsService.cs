using Microsoft.EntityFrameworkCore;
using SMS.Domain.Enums;
using SMS.Infrastructure.Exams;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Notifications;

/// <summary>Turns cross-module conditions into inbox notifications. Designed to be run on a
/// schedule (a background job) or on demand; dedupes so repeated runs don't spam.</summary>
public class AlertsService
{
    private readonly SmsDbContext _db;
    private readonly NotificationService _notify;
    private readonly ExamSchedulingService _scheduling;
    public AlertsService(SmsDbContext db, NotificationService notify, ExamSchedulingService scheduling)
    { _db = db; _notify = notify; _scheduling = scheduling; }

    public async Task<int> GenerateAsync(CancellationToken ct = default)
    {
        int created = 0;
        var now = DateTimeOffset.UtcNow;

        // Overdue open cases
        var overdue = await _db.Cases.Include(c => c.CaseType).AsNoTracking()
            .Where(c => c.ClosedUtc == null && c.DueUtc < now).ToListAsync(ct);
        foreach (var c in overdue)
            if (await _notify.CreateIfAbsentAsync($"Overdue case {c.CaseNumber}",
                    $"{c.CaseType.Name}: '{c.Title}' was due {c.DueUtc:yyyy-MM-dd}.",
                    "CaseOverdue", NotificationSeverity.Error, "Case", c.Id, c.AssignedTo, ct) is not null) created++;

        // Exam clashes in active terms
        var activeTerms = await _db.Terms.AsNoTracking().Where(t => t.IsActive).Select(t => t.Id).ToListAsync(ct);
        foreach (var termId in activeTerms)
            foreach (var clash in await _scheduling.DetectClashesAsync(termId, ct))
                if (await _notify.CreateIfAbsentAsync($"Exam clash: {clash.Type}", clash.Description,
                        "ExamClash", clash.Severity == ClashSeverity.Error ? NotificationSeverity.Error : NotificationSeverity.Warning,
                        "Exam", clash.ExamId, null, ct) is not null) created++;

        return created;
    }
}
