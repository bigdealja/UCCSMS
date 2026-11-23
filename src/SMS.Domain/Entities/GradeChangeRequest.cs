using SMS.Domain.Common;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities;

/// <summary>A proposed change to a recorded grade. Captures the ORIGINAL values (never overwritten)
/// and the proposed ones; a second person approves; the enrollment grade changes only on apply.
/// After an error, a new request is raised (a new version) rather than editing this record.</summary>
public class GradeChangeRequest : BaseEntity
{
    public int EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = default!;

    public string? OriginalLetterGrade { get; set; }
    public double? OriginalFinalMark { get; set; }
    public string? ProposedLetterGrade { get; set; }
    public double? ProposedFinalMark { get; set; }

    public GradeChangeReasonCode ReasonCode { get; set; }
    public string? Reason { get; set; }
    public GradeChangeStatus Status { get; set; } = GradeChangeStatus.Proposed;

    public string? ProposedBy { get; set; }
    public string? DecidedBy { get; set; }
    public DateTimeOffset? DecidedUtc { get; set; }
    public Guid? ApplyBatchId { get; set; }       // reuses the grade batch mechanism for traceability
}
