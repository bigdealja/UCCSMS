using SMS.Domain.Common;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities;

/// <summary>A dated rate for a type of exam work (EMS brief: rates from approved, dated rate tables).</summary>
public class PayRate : BaseEntity
{
    public WorkType WorkType { get; set; }
    public decimal RatePerUnit { get; set; }      // per hour / per script, etc.
    public string? Unit { get; set; }              // "hour" | "script" | "session"
    public DateOnly EffectiveFrom { get; set; }
    public bool IsActive { get; set; } = true;
}
