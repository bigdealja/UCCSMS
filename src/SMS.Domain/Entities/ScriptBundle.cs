using SMS.Domain.Common;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities;

/// <summary>A bundle of exam scripts with a tracked chain of custody, from collection through
/// scanning and marking to retention and witnessed disposal.</summary>
public class ScriptBundle : BaseEntity
{
    public string Code { get; set; } = default!;
    public int ExamId { get; set; }
    public Exam Exam { get; set; } = default!;
    public int ScriptCount { get; set; }
    public ScriptBundleStatus Status { get; set; } = ScriptBundleStatus.Collected;
    public string? Custodian { get; set; }
    public DateOnly? RetentionUntil { get; set; }
    public DateTimeOffset? DisposedUtc { get; set; }
    public string? DisposalWitness { get; set; }

    public ICollection<CustodyEvent> CustodyEvents { get; set; } = new List<CustodyEvent>();
}
