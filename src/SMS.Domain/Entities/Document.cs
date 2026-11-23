using SMS.Domain.Common;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities;

/// <summary>A logical document (transcript, admission letter, ID scan, …), optionally tied to a
/// student. Content is versioned — each upload adds a DocumentVersion; bytes live in object
/// storage (MinIO/S3 in production), never in the database.</summary>
public class Document : BaseEntity
{
    public string Title { get; set; } = default!;
    public DocumentCategory Category { get; set; } = DocumentCategory.Other;
    public int? StudentId { get; set; }          // null = institutional/general document
    public Student? Student { get; set; }
    public string? Description { get; set; }
    public int CurrentVersion { get; set; }

    public ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();
}
