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

public record ExamDto(int Id, string Title, int CourseId, int TermId, string? ScheduledStart,
                      int DurationMinutes, string? Room, double TotalMarks, string Status, int QuestionCount);
public record UpsertExamDto(string Title, int CourseId, int TermId, string? ScheduledStart,
                            int DurationMinutes, string? Room);
public record QuestionDto(int Id, int CourseId, string Type, string Text, double DefaultMarks,
                          string? OptionsJson, string? CorrectAnswer);
public record UpsertQuestionDto(int CourseId, string Type, string Text, double DefaultMarks,
                                string? OptionsJson, string? CorrectAnswer);
public record AddExamQuestionDto(int QuestionId, double? Marks);
public record RecordResultDto(int StudentId, double Score);

public record DocumentDto(int Id, string Title, string Category, int? StudentId, string? Description, int CurrentVersion);
