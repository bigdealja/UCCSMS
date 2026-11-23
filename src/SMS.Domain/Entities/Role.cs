using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>A configurable role (EMS brief: roles with permissions, limited by campus/school).</summary>
public class Role : BaseEntity
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();
}
