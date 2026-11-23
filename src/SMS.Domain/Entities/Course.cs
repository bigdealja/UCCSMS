using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>A catalogue course (legacy: Courses / sms_ACA_V_StudentCourses).</summary>
public class Course : BaseEntity
{
    public string Code { get; set; } = default!;
    public string Title { get; set; } = default!;
    public int Credits { get; set; }
    public int ProgrammeId { get; set; }
    public Programme Programme { get; set; } = default!;
}
