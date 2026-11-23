using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>One stored revision of a document. StorageKey points into object storage;
/// Sha256 lets us verify integrity and detect duplicate uploads.</summary>
public class DocumentVersion : BaseEntity
{
    public int DocumentId { get; set; }
    public Document Document { get; set; } = default!;
    public int VersionNumber { get; set; }
    public string StorageKey { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = default!;
    public string? UploadedBy { get; set; }
}
