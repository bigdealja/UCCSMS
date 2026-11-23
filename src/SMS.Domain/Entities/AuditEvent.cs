using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>An append-only audit record: who did what, when, and the before/after values.
/// Never updated or deleted (the EMS brief: history cannot be edited). Also used for access
/// logging (views/downloads of restricted records).</summary>
public class AuditEvent : BaseEntity
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string Actor { get; set; } = "system";
    public string Action { get; set; } = default!;      // Create | Update | Delete | View | Download
    public string EntityType { get; set; } = default!;
    public string? EntityId { get; set; }
    public string? Before { get; set; }                  // JSON of changed properties' old values
    public string? After { get; set; }                   // JSON of changed properties' new values
    public string? Reason { get; set; }
}
