using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>Grants a role to a user (by username), optionally scoped to a campus.
/// Roles may also arrive from the identity provider's token claims.</summary>
public class UserRoleAssignment : BaseEntity
{
    public string Username { get; set; } = default!;
    public string RoleName { get; set; } = default!;
    public int? CampusId { get; set; }     // optional scope (foundation for row-level scoping)
}
