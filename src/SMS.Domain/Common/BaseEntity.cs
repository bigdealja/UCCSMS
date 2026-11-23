namespace SMS.Domain.Common;

/// <summary>Base for all persisted entities. Provides identity and a full audit trail
/// (one of the gaps in the legacy Aeorion apps, which audited only logins).</summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTimeOffset? ModifiedUtc { get; set; }
    public string? ModifiedBy { get; set; }
    public bool IsDeleted { get; set; }   // soft-delete instead of hard DELETE
}
