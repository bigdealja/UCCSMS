using SMS.Domain.Entities;
using SMS.Domain.Enums;

namespace SMS.Infrastructure.DataMigration;

/// <summary>Pure transforms from legacy rows to new domain entities (no DB access — unit-testable).
/// The runner resolves foreign keys (programme, school, …) from natural-key lookups.</summary>
public static class Mappers
{
    public static ProgrammeLevel MapLevel(string? legacy) => (legacy ?? "").Trim().ToLowerInvariant() switch
    {
        "masters" or "graduate" or "msc" or "mba" => ProgrammeLevel.Masters,
        "associate" or "occupational associate" => ProgrammeLevel.OccupationalAssociate,
        "diploma" or "certificate" or "occupational diploma" => ProgrammeLevel.OccupationalDiploma,
        _ => ProgrammeLevel.Bachelors
    };

    public static StudentStatus MapStudentStatus(string? legacy) => (legacy ?? "").Trim().ToLowerInvariant() switch
    {
        "active" or "a" or "registered" => StudentStatus.Active,
        "inactive" or "i" => StudentStatus.Inactive,
        "graduated" or "g" => StudentStatus.Graduated,
        "withdrawn" or "w" => StudentStatus.Withdrawn,
        "suspended" => StudentStatus.Suspended,
        "applicant" => StudentStatus.Applicant,
        _ => StudentStatus.Active
    };

    public static TermType MapTermType(string? legacy) => (legacy ?? "").Trim().ToLowerInvariant() switch
    {
        "trimester" => TermType.Trimester, "summer" => TermType.Summer, "intersession" => TermType.Intersession,
        _ => TermType.Semester
    };

    public static Student ToStudent(LegacyStudent r, int programmeId) => new()
    {
        StudentNumber = r.StudentNumber.Trim(), FirstName = r.FirstName.Trim(), LastName = r.LastName.Trim(),
        Email = string.IsNullOrWhiteSpace(r.Email) ? null : r.Email.Trim(), DateOfBirth = r.DateOfBirth,
        Status = MapStudentStatus(r.Status), ProgrammeId = programmeId
    };

    public static Course ToCourse(LegacyCourse r, int programmeId) => new()
    { Code = r.Code.Trim(), Title = r.Title.Trim(), Credits = r.Credits, ProgrammeId = programmeId };

    public static Programme ToProgramme(LegacyProgramme r, int schoolId) => new()
    { Code = r.Code.Trim(), Name = r.Name.Trim(), SchoolId = schoolId, CreditsRequired = r.CreditsRequired, Level = MapLevel(r.Level) };

    public static Term ToTerm(LegacyTerm r) => new()
    {
        Name = r.Name.Trim(), Year = r.Year, Type = MapTermType(r.Type), IsActive = r.IsActive,
        StartDate = r.StartDate ?? default, EndDate = r.EndDate ?? default
    };

    // ----- Finance -----
    public static TransactionType MapTransactionType(string? legacy) => (legacy ?? "").Trim().ToLowerInvariant() switch
    {
        "payment" or "pay" or "pmt" or "receipt" or "cr" or "credit" => TransactionType.Payment,
        "refund" or "rfd" => TransactionType.Refund,
        "adjustment" or "adj" or "journal" => TransactionType.Adjustment,
        _ => TransactionType.Charge
    };

    public static PaymentMethod? MapPaymentMethod(string? legacy) => (legacy ?? "").Trim().ToLowerInvariant() switch
    {
        "" => null,
        "cash" => PaymentMethod.Cash,
        "card" or "credit card" or "debit card" or "pos" => PaymentMethod.Card,
        "bank" or "transfer" or "banktransfer" or "wire" or "ach" => PaymentMethod.BankTransfer,
        "cheque" or "check" or "chk" => PaymentMethod.Cheque,
        "scholarship" => PaymentMethod.Scholarship,
        "waiver" => PaymentMethod.Waiver,
        _ => PaymentMethod.Other
    };

    /// <summary>Normalise a ledger amount to the new schema's sign convention: charges positive,
    /// payments/refunds negative; adjustments keep the legacy sign.</summary>
    public static decimal SignedAmount(TransactionType type, decimal amount) => type switch
    {
        TransactionType.Payment or TransactionType.Refund => -Math.Abs(amount),
        TransactionType.Charge => Math.Abs(amount),
        _ => amount
    };

    public static FeeType ToFeeType(LegacyFeeType r) => new()
    { Code = r.Code.Trim(), Name = r.Name.Trim(), DefaultAmount = r.DefaultAmount,
      DistributionCode = string.IsNullOrWhiteSpace(r.DistributionCode) ? null : r.DistributionCode.Trim() };

    public static FinancialTransaction ToFinanceTxn(LegacyFinanceTxn r, int studentId, int? feeTypeId, int? termId)
    {
        var type = MapTransactionType(r.Type);
        return new FinancialTransaction
        {
            StudentId = studentId, FeeTypeId = feeTypeId, TermId = termId,
            Type = type, Amount = SignedAmount(type, r.Amount),
            Method = MapPaymentMethod(r.Method),
            Reference = string.IsNullOrWhiteSpace(r.Reference) ? null : r.Reference.Trim(),
            Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim(),
            PostedUtc = r.PostedUtc ?? DateTimeOffset.UtcNow,
            AccountingStatus = AccountingSyncStatus.NotApplicable   // historical rows are not re-posted
        };
    }

    // ----- Admissions -----
    public static LeadStatus MapLeadStatus(string? legacy) => (legacy ?? "").Trim().ToLowerInvariant() switch
    {
        "contacted" => LeadStatus.Contacted,
        "qualified" => LeadStatus.Qualified,
        "applicationstarted" or "application started" or "applied" => LeadStatus.ApplicationStarted,
        "converted" or "enrolled" => LeadStatus.Converted,
        "lost" or "dead" or "closed" => LeadStatus.Lost,
        _ => LeadStatus.New
    };

    public static ApplicationStatus MapApplicationStatus(string? legacy) => (legacy ?? "").Trim().ToLowerInvariant() switch
    {
        "submitted" => ApplicationStatus.Submitted,
        "underreview" or "under review" or "review" or "in review" => ApplicationStatus.UnderReview,
        "documentsoutstanding" or "documents outstanding" or "incomplete" => ApplicationStatus.DocumentsOutstanding,
        "decisionpending" or "decision pending" or "pending" => ApplicationStatus.DecisionPending,
        "accepted" or "admitted" or "offer" => ApplicationStatus.Accepted,
        "declined" or "rejected" or "denied" => ApplicationStatus.Declined,
        "deferred" => ApplicationStatus.Deferred,
        "enrolled" or "registered" => ApplicationStatus.Enrolled,
        "withdrawn" or "cancelled" or "canceled" => ApplicationStatus.Withdrawn,
        _ => ApplicationStatus.Draft
    };

    public static AdmissionCategory MapAdmissionCategory(string? legacy) => (legacy ?? "").Trim().ToLowerInvariant() switch
    {
        "full" or "fullacceptance" or "full acceptance" => AdmissionCategory.FullAcceptance,
        "fullacceptancetodocore" or "full to do core" => AdmissionCategory.FullAcceptanceToDoCore,
        "conditional1" or "conditional class 1" or "conditionalclass1" => AdmissionCategory.ConditionalClass1,
        "conditional2" or "conditional class 2" or "conditionalclass2mature" => AdmissionCategory.ConditionalClass2Mature,
        "mature" or "matureentrant" or "mature entrant" => AdmissionCategory.MatureEntrant,
        "provisional" => AdmissionCategory.Provisional,
        "conditionalgraduate" or "conditional graduate" => AdmissionCategory.ConditionalGraduate,
        _ => AdmissionCategory.None
    };

    public static Lead ToLead(LegacyLead r, int? programmeOfInterestId) => new()
    {
        FirstName = r.FirstName.Trim(), LastName = r.LastName.Trim(),
        Email = string.IsNullOrWhiteSpace(r.Email) ? null : r.Email.Trim(),
        Phone = string.IsNullOrWhiteSpace(r.Phone) ? null : r.Phone.Trim(),
        Source = string.IsNullOrWhiteSpace(r.Source) ? null : r.Source.Trim(),
        Status = MapLeadStatus(r.Status), ProgrammeOfInterestId = programmeOfInterestId,
        AssignedTo = string.IsNullOrWhiteSpace(r.AssignedTo) ? null : r.AssignedTo.Trim(),
        ExternalCrmId = r.ExternalRef.Trim()
    };

    public static AdmissionApplication ToApplication(LegacyApplication r, int programmeId, int intakeTermId, int? leadId) => new()
    {
        ApplicationNumber = r.ApplicationNumber.Trim(), LeadId = leadId,
        FirstName = r.FirstName.Trim(), LastName = r.LastName.Trim(),
        Email = string.IsNullOrWhiteSpace(r.Email) ? null : r.Email.Trim(), DateOfBirth = r.DateOfBirth,
        ProgrammeId = programmeId, IntakeTermId = intakeTermId,
        Status = MapApplicationStatus(r.Status), Decision = MapAdmissionCategory(r.Decision),
        SubmittedUtc = r.SubmittedUtc, DecidedUtc = r.DecidedUtc,
        AssignedOfficer = string.IsNullOrWhiteSpace(r.AssignedOfficer) ? null : r.AssignedOfficer.Trim(),
        DecidedBy = string.IsNullOrWhiteSpace(r.DecidedBy) ? null : r.DecidedBy.Trim()
    };
}
