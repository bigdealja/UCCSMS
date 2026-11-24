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
}
