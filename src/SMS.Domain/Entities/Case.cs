using SMS.Domain.Common;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities;

/// <summary>A unit of staff work: a query, appeal, grade amendment, irregularity, remark, etc.
/// Due date is computed from the case type's working-day turnaround; status (incl. overdue/
/// due-soon) is worked out from the dates, not typed in.</summary>
public class Case : BaseEntity
{
    public string CaseNumber { get; set; } = default!;
    public int CaseTypeId { get; set; }
    public CaseType CaseType { get; set; } = default!;

    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public CaseStatus Status { get; set; } = CaseStatus.Open;
    public CasePriority Priority { get; set; } = CasePriority.Normal;

    public int? StudentId { get; set; }
    public Student? Student { get; set; }

    public string? RaisedBy { get; set; }
    public string? AssignedTo { get; set; }
    public DateTimeOffset DueUtc { get; set; }

    public string? Resolution { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ClosedUtc { get; set; }
    public string? CancelReason { get; set; }

    public ICollection<CaseAction> Actions { get; set; } = new List<CaseAction>();
}
