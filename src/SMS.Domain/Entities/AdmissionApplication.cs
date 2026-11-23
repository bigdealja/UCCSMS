using SMS.Domain.Common;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities;

/// <summary>An admission application moving through the UCC Admissions Policy workflow:
/// submit → review → document verification → decision → offer response → enrolment.
/// On acceptance it produces a Student (linking the admissions funnel to registration).</summary>
public class AdmissionApplication : BaseEntity
{
    public string ApplicationNumber { get; set; } = default!;
    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }

    // Applicant details (before a Student record exists)
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string? Email { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public bool IsMatureEntrant { get; set; }
    public bool IsInternational { get; set; }

    public int ProgrammeId { get; set; }
    public Programme Programme { get; set; } = default!;
    public int IntakeTermId { get; set; }
    public Term IntakeTerm { get; set; } = default!;

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Draft;
    public DateTimeOffset? SubmittedUtc { get; set; }
    public string? AssignedOfficer { get; set; }

    // Decision (second-person approval enforced in the service)
    public AdmissionCategory Decision { get; set; } = AdmissionCategory.None;
    public string? DecidedBy { get; set; }
    public DateTimeOffset? DecidedUtc { get; set; }
    public string? DecisionNotes { get; set; }

    // Placement Offer Response Form
    public OfferResponse OfferResponse { get; set; } = OfferResponse.Pending;

    // Result of acceptance
    public int? StudentId { get; set; }
    public Student? Student { get; set; }

    public ICollection<ApplicationDocument> Documents { get; set; } = new List<ApplicationDocument>();
}
