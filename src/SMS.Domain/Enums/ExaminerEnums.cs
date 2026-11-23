namespace SMS.Domain.Enums;

public enum ExaminerStatus { Prospective, Appointed, Active, Expired, Terminated }

/// <summary>The 11-stage external-examiner assignment/package workflow (EMS brief), plus Cancelled.</summary>
public enum AssignmentStage
{
    Nominated, ConflictCheck, Appointed, ContractIssued, ContractSigned,
    PackagePreparation, PackageDispatched, UnderReview, ReportReceived, ActionPlan, Completed, Cancelled
}
