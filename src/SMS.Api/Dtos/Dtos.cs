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

public record FeeTypeDto(int Id, string Code, string Name, decimal DefaultAmount, string? DistributionCode);
public record UpsertFeeTypeDto(string Code, string Name, decimal DefaultAmount, string? DistributionCode);
public record ChargeDto(int? TermId, int? FeeTypeId, decimal Amount, string? Description);
public record PaymentDto(decimal Amount, string Method, string? Reference, string? Description, bool IsRefund = false);

public record GradeBandDto(string Letter, double? MinPercent, double? MaxPercent, double QualityPoints, bool IsPass, bool CountsInGpa, bool IsSpecial);
public record GradeSchemeDto(int Id, string Name, string Level, string EffectiveFrom, double OverallPassMark, double CourseworkPassMark, double EosaPassMark, IReadOnlyList<GradeBandDto> Bands);

public record LeadDto(int Id, string FirstName, string LastName, string? Email, string? Phone, string? Source, string Status, int? ProgrammeOfInterestId);
public record UpsertLeadDto(string FirstName, string LastName, string? Email, string? Phone, string? Source, int? ProgrammeOfInterestId);
public record ConvertLeadDto(int ProgrammeId, int IntakeTermId);
public record ApplicationDto(int Id, string ApplicationNumber, string FirstName, string LastName, string? Email, int ProgrammeId, int IntakeTermId, string Status, string Decision, string OfferResponse, string? AssignedOfficer, string? DecidedBy, int? StudentId);
public record DecisionDto(string Category, string? DecidedBy, string? Notes);
public record OfferResponseDto(string Response);
public record ConvertToStudentDto(string StudentNumber);

public record PublicLeadDto(string FirstName, string LastName, string? Email, string? Phone, string? Source, int? ProgrammeOfInterestId);

public record RoomDto(int Id, string Code, string Name, int CampusId, int Capacity);
public record UpsertRoomDto(string Code, string Name, int CampusId, int Capacity);
public record AllocateRoomDto(int RoomId, int Seats);
public record AssignInvigilatorDto(int StaffFacultyId, string Role);
public record ArrangementDto(int Id, int StudentId, string Type, int ExtraTimePercent, string? Details, bool Approved);
public record UpsertArrangementDto(int StudentId, string Type, int ExtraTimePercent, string? Details, bool Approved);

public record CaseTypeDto(int Id, string Code, string Name, int TurnaroundWorkingDays, bool RequiresSecondPersonApproval);
public record CaseDto(int Id, string CaseNumber, string Type, string Title, string Status, string Priority, int? StudentId, string? AssignedTo, string? RaisedBy, string DueUtc, bool Overdue, bool DueSoon);
public record CreateCaseDto(int CaseTypeId, string Title, string? Description, int? StudentId, string Priority);

public record AskDto(string Question, int? StudentId);
public record KnowledgeArticleDto(int Id, string Title, string Category, string Body, string? Tags);
public record UpsertArticleDto(string Title, string Category, string Body, string? Tags);
