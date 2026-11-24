namespace SMS.Infrastructure.DataMigration;

// Flat row shapes read from the legacy Aeorion databases (names mirror the reverse-engineered
// tables/views, e.g. sms_SI_V_Students, sms_ADM_D_Term, Courses). Natural keys (codes/numbers)
// are used to resolve relationships during load.
public record LegacyCampus(string Code, string Name);
public record LegacySchool(string Code, string Name, string CampusCode);
public record LegacyProgramme(string Code, string Name, string SchoolCode, int CreditsRequired, string? Level);
public record LegacyTerm(string Name, int Year, string? Type, DateOnly? StartDate, DateOnly? EndDate, bool IsActive);
public record LegacyCourse(string Code, string Title, int Credits, string ProgrammeCode);
public record LegacyStudent(string StudentNumber, string FirstName, string LastName, string? Email,
    DateOnly? DateOfBirth, string? Status, string ProgrammeCode);
public record LegacyEnrollment(string StudentNumber, string CourseCode, string TermName, int TermYear,
    string? LetterGrade, double? CourseworkMark, double? FinalExamMark);

// ----- Finance history (legacy ASM/finance ledger) -----
public record LegacyFeeType(string Code, string Name, decimal DefaultAmount, string? DistributionCode);
public record LegacyFinanceTxn(string StudentNumber, string? Reference, string? FeeTypeCode,
    string? Type, decimal Amount, string? Method, string? Description,
    DateTimeOffset? PostedUtc, string? TermName, int? TermYear);

// ----- Admissions history (legacy SMS_Apply) -----
public record LegacyLead(string ExternalRef, string FirstName, string LastName, string? Email,
    string? Phone, string? Source, string? Status, string? ProgrammeCode, string? AssignedTo);
public record LegacyApplication(string ApplicationNumber, string FirstName, string LastName, string? Email,
    DateOnly? DateOfBirth, string ProgrammeCode, string? IntakeTermName, int? IntakeTermYear,
    string? Status, string? Decision, string? AssignedOfficer, string? DecidedBy,
    DateTimeOffset? SubmittedUtc, DateTimeOffset? DecidedUtc, string? LeadExternalRef);
