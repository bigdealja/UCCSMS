using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>A required/received supporting document for an application (birth certificate,
/// transcripts, qualifications, photo, references …). Verification per the policy's
/// Document Verification SOP. Links to the stored file in the Documents module when uploaded.</summary>
public class ApplicationDocument : BaseEntity
{
    public int ApplicationId { get; set; }
    public AdmissionApplication AdmissionApplication { get; set; } = default!;
    public string DocType { get; set; } = default!;        // "BirthCertificate", "Transcript", "Photo", "Qualification", "Reference", "Resume"
    public bool Required { get; set; } = true;
    public bool Received { get; set; }
    public bool Verified { get; set; }
    public int? DocumentId { get; set; }                   // -> Documents module (stored file)
    public string? Notes { get; set; }
}
