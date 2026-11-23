using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>A marked-script file uploaded by a marker for a bundle (links to the Documents store).</summary>
public class MarkedScriptUpload : BaseEntity
{
    public int ScriptBundleId { get; set; }
    public ScriptBundle ScriptBundle { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public int? DocumentId { get; set; }
    public string? UploadedBy { get; set; }
}
