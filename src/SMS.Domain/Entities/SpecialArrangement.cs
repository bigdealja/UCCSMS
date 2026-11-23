using SMS.Domain.Common;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities;

/// <summary>An approved special exam arrangement for a student (extra time, separate room, reader,
/// scribe, assistive technology, …). Extra time extends that student's effective exam duration,
/// which the clash checker accounts for.</summary>
public class SpecialArrangement : BaseEntity
{
    public int StudentId { get; set; }
    public Student Student { get; set; } = default!;
    public SpecialArrangementType Type { get; set; }
    public int ExtraTimePercent { get; set; }        // for ExtraTime, e.g. 25
    public string? Details { get; set; }
    public bool Approved { get; set; }
    public string? ApprovedBy { get; set; }
    public DateOnly? EffectiveFrom { get; set; }
}
