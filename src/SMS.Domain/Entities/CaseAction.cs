using SMS.Domain.Common;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities;

/// <summary>An immutable entry on a case's history (note, status change, approval, …).</summary>
public class CaseAction : BaseEntity
{
    public int CaseId { get; set; }
    public Case Case { get; set; } = default!;
    public CaseActionType Type { get; set; }
    public string? Text { get; set; }
    public string? By { get; set; }
    public int? LinkedCaseId { get; set; }
}
