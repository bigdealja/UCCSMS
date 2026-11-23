using SMS.Domain.Common;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities;

/// <summary>A versioned grading scale (UCC Examination Policy, Section XII). Configurable with an
/// effective date — both the policy and the EMS brief require grade schemes to be settings with
/// effective dates and approval, never hard-coded.</summary>
public class GradeScheme : BaseEntity
{
    public string Name { get; set; } = default!;
    public ProgrammeLevel Level { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Overall pass mark for the combined score (50% UG/Occupational, 60% Masters).</summary>
    public double OverallPassMark { get; set; } = 50;
    /// <summary>Minimum coursework % required to pass (policy: 50%).</summary>
    public double CourseworkPassMark { get; set; } = 50;
    /// <summary>Minimum End-of-Semester Assessment % required to pass (policy: 50%).</summary>
    public double EosaPassMark { get; set; } = 50;

    public ICollection<GradeBand> Bands { get; set; } = new List<GradeBand>();
}
