namespace SMS.Domain.Enums;

/// <summary>Secure question-paper lifecycle (EMS brief). Forward-only, except Withdrawn (emergency,
/// any stage) and Destroyed (only after Returned). Overdue/at-risk are computed from the deadline.</summary>
public enum PaperStatus
{
    Expected, Submitted, UnderVetting, ExternalReview, Approved, Released,
    Printed, Dispatched, Returned, Destroyed, Withdrawn
}
