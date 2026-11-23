using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>A granular permission (e.g. "grades.change.approve") that endpoints require.</summary>
public class Permission : BaseEntity
{
    public string Key { get; set; } = default!;
    public string? Description { get; set; }
}
