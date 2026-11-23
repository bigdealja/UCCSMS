using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>A configurable case type (EMS brief: 14 types with turnaround and checkpoints).
/// Turnaround is in working days; sensitive types require second-person approval.</summary>
public class CaseType : BaseEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public int TurnaroundWorkingDays { get; set; } = 5;
    public bool RequiresSecondPersonApproval { get; set; }
    public bool IsActive { get; set; } = true;
}
