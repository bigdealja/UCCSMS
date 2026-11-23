using SMS.Domain.Common;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities;

/// <summary>An ADVISORY recommendation for an application, produced by AI or by deterministic
/// matriculation rules. It never changes the application's decision — a human approves via the
/// (second-person) decision workflow. Kept as a record for audit and transparency.</summary>
public class AdmissionRecommendation : BaseEntity
{
    public int ApplicationId { get; set; }
    public AdmissionApplication Application { get; set; } = default!;
    public AdmissionCategory RecommendedCategory { get; set; }
    public double Confidence { get; set; }                 // 0..1
    public string? Rationale { get; set; }
    public string? MissingDocuments { get; set; }           // JSON array of doc names
    public string Source { get; set; } = "Rules";           // "AI" | "Rules"
    public bool Accepted { get; set; }                       // did a human adopt it?
}
