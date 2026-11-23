namespace SMS.Api.Dtos;

public record StudentDto(int Id, string StudentNumber, string FirstName, string LastName,
                         string? Email, string Status, int ProgrammeId);

public record UpsertStudentDto(string StudentNumber, string FirstName, string LastName,
                               string? Email, string Status, int ProgrammeId, string? DateOfBirth);

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public record EnrollmentDto(int Id, int StudentId, int CourseId, int TermId, string Status,
                            string? LetterGrade, double? CourseworkMark, double? FinalExamMark,
                            Guid? ImportBatchId, string? GradeSource);

public record LookupDto(int Id, string Code, string Name);
