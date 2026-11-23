using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.AssessmentOps;

public record SittingStatus(int Id, int ExamId, string DueDate, string Stage, string Status, string? ExceptionNote, string? LateReason);

/// <summary>Assessment operations register: tracks each sitting's milestones and derives its stage
/// and status (Complete / Overdue / AtRisk / Blocked / InProgress / NotStarted) from the data.</summary>
public class AssessmentOpsService
{
    private readonly SmsDbContext _db;
    public AssessmentOpsService(SmsDbContext db) => _db = db;

    static readonly MilestoneName[] Order =
    {
        MilestoneName.ScriptsCollected, MilestoneName.Marked, MilestoneName.GradeSheetReturned,
        MilestoneName.Entered, MilestoneName.SentToDean, MilestoneName.Vetted, MilestoneName.Published
    };

    public async Task<SittingRecord> CreateAsync(int examId, DateOnly dueDate, CancellationToken ct = default)
    {
        if (!await _db.Exams.AnyAsync(e => e.Id == examId, ct)) throw new InvalidOperationException("Exam not found.");
        if (await _db.SittingRecords.AnyAsync(r => r.ExamId == examId, ct)) throw new InvalidOperationException("A sitting record already exists for this exam.");
        var r = new SittingRecord { ExamId = examId, DueDate = dueDate };
        _db.SittingRecords.Add(r); await _db.SaveChangesAsync(ct);
        return r;
    }

    static DateTimeOffset? Get(SittingRecord r, MilestoneName m) => m switch
    {
        MilestoneName.ScriptsCollected => r.ScriptsCollectedUtc,
        MilestoneName.Marked => r.MarkedUtc,
        MilestoneName.GradeSheetReturned => r.GradeSheetReturnedUtc,
        MilestoneName.Entered => r.EnteredUtc,
        MilestoneName.SentToDean => r.SentToDeanUtc,
        MilestoneName.Vetted => r.VettedUtc,
        MilestoneName.Published => r.PublishedUtc,
        _ => null
    };
    static void Set(SittingRecord r, MilestoneName m, DateTimeOffset v)
    { switch (m) {
        case MilestoneName.ScriptsCollected: r.ScriptsCollectedUtc = v; break;
        case MilestoneName.Marked: r.MarkedUtc = v; break;
        case MilestoneName.GradeSheetReturned: r.GradeSheetReturnedUtc = v; break;
        case MilestoneName.Entered: r.EnteredUtc = v; break;
        case MilestoneName.SentToDean: r.SentToDeanUtc = v; break;
        case MilestoneName.Vetted: r.VettedUtc = v; break;
        case MilestoneName.Published: r.PublishedUtc = v; break;
    } }

    /// <summary>Record a milestone. Milestones are sequential — the prior one must be complete.</summary>
    public async Task RecordMilestoneAsync(int recordId, MilestoneName milestone, CancellationToken ct = default)
    {
        var r = await _db.SittingRecords.FirstOrDefaultAsync(x => x.Id == recordId, ct)
                ?? throw new InvalidOperationException("Sitting record not found.");
        int idx = Array.IndexOf(Order, milestone);
        if (idx > 0 && Get(r, Order[idx - 1]) is null)
            throw new InvalidOperationException($"Previous milestone '{Order[idx - 1]}' is not complete.");
        if (Get(r, milestone) is not null) return; // idempotent
        Set(r, milestone, DateTimeOffset.UtcNow);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetExceptionAsync(int recordId, string? note, CancellationToken ct = default)
    {
        var r = await _db.SittingRecords.FirstOrDefaultAsync(x => x.Id == recordId, ct)
                ?? throw new InvalidOperationException("Sitting record not found.");
        r.ExceptionNote = note; await _db.SaveChangesAsync(ct);
    }

    public async Task SetLateReasonAsync(int recordId, string reason, CancellationToken ct = default)
    {
        var r = await _db.SittingRecords.FirstOrDefaultAsync(x => x.Id == recordId, ct)
                ?? throw new InvalidOperationException("Sitting record not found.");
        r.LateReason = reason; await _db.SaveChangesAsync(ct);
    }

    public static (string Stage, string Status) Evaluate(SittingRecord r)
    {
        var stage = "NotStarted";
        foreach (var m in Order) if (Get(r, m) is not null) stage = m.ToString();

        if (r.PublishedUtc is not null) return (stage, "Complete");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (!string.IsNullOrEmpty(r.ExceptionNote)) return (stage, "Blocked");
        if (r.DueDate < today) return (stage, "Overdue");
        if (r.DueDate < today.AddDays(3)) return (stage, "AtRisk");
        return (stage, stage == "NotStarted" ? "NotStarted" : "InProgress");
    }

    public async Task<List<SittingStatus>> ListAsync(int? termId, bool needsAttentionOnly, CancellationToken ct = default)
    {
        var q = _db.SittingRecords.Include(r => r.Exam).AsNoTracking().AsQueryable();
        if (termId is int t) q = q.Where(r => r.Exam.TermId == t);
        var recs = await q.ToListAsync(ct);
        var result = recs.Select(r => { var (stage, status) = Evaluate(r);
            return new SittingStatus(r.Id, r.ExamId, r.DueDate.ToString("yyyy-MM-dd"), stage, status, r.ExceptionNote, r.LateReason); }).ToList();
        if (needsAttentionOnly)
            result = result.Where(x => x.Status is "Overdue" or "AtRisk" or "Blocked").ToList();
        return result.OrderBy(x => x.DueDate).ToList();
    }
}
