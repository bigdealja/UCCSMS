using Microsoft.EntityFrameworkCore;
using SMS.Domain.Enums;
using SMS.Infrastructure.Exams;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Notifications;

/// <summary>Computes a cross-module KPI snapshot — the EMS principle that reports are worked out
/// from records, never typed in.</summary>
public class MetricsService
{
    private readonly SmsDbContext _db;
    private readonly ExamSchedulingService _scheduling;
    public MetricsService(SmsDbContext db, ExamSchedulingService scheduling) { _db = db; _scheduling = scheduling; }

    public async Task<object> OverviewAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var studentsByStatus = await _db.Students.AsNoTracking()
            .GroupBy(s => s.Status).Select(g => new { status = g.Key.ToString(), count = g.Count() }).ToListAsync(ct);

        int leads = await _db.Leads.CountAsync(ct);
        var appsByStatus = await _db.Applications.AsNoTracking()
            .GroupBy(a => a.Status).Select(g => new { status = g.Key.ToString(), count = g.Count() }).ToListAsync(ct);
        int appsTotal = appsByStatus.Sum(x => x.count);
        int enrolled = await _db.Applications.CountAsync(a => a.Status == ApplicationStatus.Enrolled, ct);

        var openCases = _db.Cases.Where(c => c.ClosedUtc == null);
        int casesOpen = await openCases.CountAsync(ct);
        int casesOverdue = await openCases.CountAsync(c => c.DueUtc < now, ct);

        int activeTerm = await _db.Terms.Where(t => t.IsActive).Select(t => t.Id).FirstOrDefaultAsync(ct);
        int examsScheduled = await _db.Exams.CountAsync(e => e.TermId == activeTerm && e.ScheduledStart != null, ct);
        int clashes = activeTerm == 0 ? 0 : (await _scheduling.DetectClashesAsync(activeTerm, ct)).Count;

        decimal ledgerBalance = await _db.FinancialTransactions.SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;

        return new
        {
            students = new { total = studentsByStatus.Sum(x => x.count), byStatus = studentsByStatus },
            admissions = new { leads, applications = appsTotal, byStatus = appsByStatus, enrolled,
                               conversionRate = appsTotal == 0 ? 0 : Math.Round(100.0 * enrolled / appsTotal, 1) },
            cases = new { open = casesOpen, overdue = casesOverdue },
            exams = new { scheduled = examsScheduled, clashes },
            finance = new { ledgerBalance }
        };
    }
}
