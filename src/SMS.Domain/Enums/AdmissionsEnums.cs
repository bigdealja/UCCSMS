namespace SMS.Domain.Enums;

/// <summary>CRM lead pipeline (front of the funnel — web capture via Mautic, events, referrals).</summary>
public enum LeadStatus { New, Contacted, Qualified, ApplicationStarted, Converted, Lost }

/// <summary>Admission application lifecycle, from the UCC Admissions Policy processing steps.</summary>
public enum ApplicationStatus
{
    Draft, Submitted, UnderReview, DocumentsOutstanding,
    DecisionPending, Accepted, Declined, Deferred, Enrolled, Withdrawn
}

/// <summary>Admission categories from the policy (Categories of Admission).</summary>
public enum AdmissionCategory
{
    None, FullAcceptance, FullAcceptanceToDoCore,
    ConditionalClass1, ConditionalClass2Mature, MatureEntrant,   // undergraduate
    Provisional, ConditionalGraduate                              // graduate
}

/// <summary>Applicant's answer on the Placement Offer Response Form.</summary>
public enum OfferResponse { Pending, Accepted, Declined, Deferred }
