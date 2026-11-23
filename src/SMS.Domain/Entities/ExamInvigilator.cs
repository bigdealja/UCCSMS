using SMS.Domain.Common;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities;

/// <summary>Assigns an invigilator (staff/faculty) to an exam, with a role.</summary>
public class ExamInvigilator : BaseEntity
{
    public int ExamId { get; set; }
    public Exam Exam { get; set; } = default!;
    public int StaffFacultyId { get; set; }
    public StaffFaculty StaffFaculty { get; set; } = default!;
    public InvigilatorRole Role { get; set; } = InvigilatorRole.Assistant;
}
