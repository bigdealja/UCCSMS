using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>A student's raw mark for one assessment component (out of the component's MaxMark).</summary>
public class ComponentMark : BaseEntity
{
    public int AssessmentComponentId { get; set; }
    public AssessmentComponent AssessmentComponent { get; set; } = default!;
    public int StudentId { get; set; }
    public Student Student { get; set; } = default!;
    public double Mark { get; set; }
    public string? EnteredBy { get; set; }
    public string? Source { get; set; }   // "Manual" | "Moodle"
}
