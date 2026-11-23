using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>A degree/programme of study (legacy: StudentPrograms / ProgramStatus).</summary>
public class Programme : BaseEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public int SchoolId { get; set; }
    public School School { get; set; } = default!;
    public int CreditsRequired { get; set; }
    public ICollection<Course> Courses { get; set; } = new List<Course>();
}
