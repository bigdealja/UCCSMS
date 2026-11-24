using SMS.Domain.Common;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities;

/// <summary>A claim for completed exam work (marking, invigilation, external examining). The amount
/// is computed from the dated rate; it is approved by a second person and then exported to HR/Finance.</summary>
public class WorkClaim : BaseEntity
{
    public string ClaimantName { get; set; } = default!;
    public int? StaffFacultyId { get; set; }
    public WorkType WorkType { get; set; }
    public decimal Units { get; set; }
    public decimal RatePerUnit { get; set; }      // snapshot of the rate applied
    public decimal Amount { get; set; }
    public string? CompletionEvidence { get; set; }

    public ClaimStatus Status { get; set; } = ClaimStatus.Draft;
    public string? SubmittedBy { get; set; }
    public string? ApprovedBy { get; set; }
    public string? ExternalRef { get; set; }       // reference from the HR/Finance system
    public string? Provider { get; set; }
}
