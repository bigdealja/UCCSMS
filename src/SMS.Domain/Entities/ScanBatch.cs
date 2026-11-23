using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>A scanning batch for a bundle; the scanned collection can be shared with lecturers.</summary>
public class ScanBatch : BaseEntity
{
    public int ScriptBundleId { get; set; }
    public ScriptBundle ScriptBundle { get; set; } = default!;
    public int ScannedCount { get; set; }
    public string? CollectionRef { get; set; }
    public string? ScannedBy { get; set; }
}
