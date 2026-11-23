using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>An external examiner's report for an assignment, with the Dean/HOD action plan and
/// corrective actions tracked to completion.</summary>
public class ExaminerReport : BaseEntity
{
    public int AssignmentId { get; set; }
    public ExaminerAssignment Assignment { get; set; } = default!;
    public string Summary { get; set; } = default!;
    public string? Recommendations { get; set; }
    public DateTimeOffset SubmittedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? SubmittedBy { get; set; }

    public string? ActionPlan { get; set; }
    public string? ActionOwner { get; set; }
    public string? CorrectiveActions { get; set; }
    public bool ActionsComplete { get; set; }
}
