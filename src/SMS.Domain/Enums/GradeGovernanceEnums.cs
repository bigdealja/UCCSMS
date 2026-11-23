namespace SMS.Domain.Enums;

/// <summary>Why a grade is being changed (EMS brief: grade changes carry reason codes).</summary>
public enum GradeChangeReasonCode { ClericalError, RemarkOutcome, AppealOutcome, LateCoursework, IncompleteCleared, IntegrityOutcome, Other }
public enum GradeChangeStatus { Proposed, Applied, Rejected }
