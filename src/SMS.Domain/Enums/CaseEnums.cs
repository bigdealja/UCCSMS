namespace SMS.Domain.Enums;

public enum CaseStatus { Open, InProgress, PendingApproval, Approved, Rejected, Resolved, Cancelled }
public enum CasePriority { Low, Normal, High, Urgent }
public enum CaseActionType { Note, StatusChange, Approval, Assignment, Cancellation, Link }
