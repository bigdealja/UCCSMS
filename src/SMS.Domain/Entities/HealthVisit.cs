using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>A student health-services visit. Confidential by default — access is restricted and
/// views are logged by the audit layer.</summary>
public class HealthVisit : BaseEntity
{
    public int StudentId { get; set; }
    public Student Student { get; set; } = default!;
    public DateTimeOffset VisitUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Reason { get; set; } = default!;
    public string? Notes { get; set; }
    public string? SeenBy { get; set; }
    public bool Confidential { get; set; } = true;
}
