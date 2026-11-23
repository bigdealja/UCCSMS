using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>One row of a grading scale: a letter grade for a percentage range, with its GPA
/// quality points. Special grades (I, RE, DNS, W, …) have no percentage range and may be excluded
/// from the GPA per policy.</summary>
public class GradeBand : BaseEntity
{
    public int GradeSchemeId { get; set; }
    public GradeScheme GradeScheme { get; set; } = default!;
    public string Letter { get; set; } = default!;
    public double? MinPercent { get; set; }   // null for special grades
    public double? MaxPercent { get; set; }
    public double QualityPoints { get; set; }
    public bool IsPass { get; set; }
    public bool CountsInGpa { get; set; } = true;
    public bool IsSpecial { get; set; }        // I, RE, DNS, W, WF, AB, DEF, MED, AM, EC
    public int SortOrder { get; set; }
}
