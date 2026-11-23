using SMS.Domain.Common;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities;

/// <summary>Core student record (legacy: sms_SI_V_Students, StudentSummary, Addresses, StudentEmails).
/// StudentNumber is the institution-facing ID; the legacy apps juggled "old ID"/"new ID" mappings
/// (frmAssignStudentoldID) — we keep one stable key and avoid that problem.</summary>
public class Student : BaseEntity
{
    public string StudentNumber { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string? Email { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public StudentStatus Status { get; set; } = StudentStatus.Applicant;

    public int ProgrammeId { get; set; }
    public Programme Programme { get; set; } = default!;

    /// <summary>Linked Moodle user id, set when the student is provisioned to Moodle.</summary>
    public long? MoodleUserId { get; set; }

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}
