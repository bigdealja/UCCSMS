using SMS.Domain.Common;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities;

/// <summary>A gradeable assessment component for a course sitting (e.g. "Assignment 1", "Mid-semester",
/// "Final Exam"), with its weight toward the final mark. Replaces the external grade sheet: marks are
/// entered against components and the system calculates the result.</summary>
public class AssessmentComponent : BaseEntity
{
    public int CourseId { get; set; }
    public Course Course { get; set; } = default!;
    public int TermId { get; set; }
    public Term Term { get; set; } = default!;
    public string Name { get; set; } = default!;
    public GradeCategory Category { get; set; }
    public double Weight { get; set; }        // share of the final 100%
    public double MaxMark { get; set; } = 100;
    public string? MoodleItemId { get; set; }  // optional mapping to a Moodle grade item
}
