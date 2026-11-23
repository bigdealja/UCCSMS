using SMS.Domain.Common;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities;

/// <summary>A prospective student captured before they apply (web form via Mautic, event, referral).
/// The start of the student lifecycle. Converts into an Application.</summary>
public class Lead : BaseEntity
{
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Source { get; set; }              // "Website", "Mautic", "Event", "Referral"
    public int? ProgrammeOfInterestId { get; set; }
    public Programme? ProgrammeOfInterest { get; set; }
    public LeadStatus Status { get; set; } = LeadStatus.New;
    public string? AssignedTo { get; set; }
    public string? ExternalCrmId { get; set; }        // id in Mautic/Salesforce/etc.
}
