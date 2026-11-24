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
