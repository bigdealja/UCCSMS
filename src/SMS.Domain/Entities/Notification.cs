using SMS.Domain.Common;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities;

/// <summary>An alert/message in a user's (or a role's) inbox. Generated from events and rules
/// across the platform — overdue cases, exam clashes, admissions milestones — never typed twice.
/// RefType/RefId point back at the record that triggered it (and dedupe repeat alerts).</summary>
public class Notification : BaseEntity
{
    public string? Recipient { get; set; }        // username; null = broadcast / role-wide
    public string Title { get; set; } = default!;
    public string? Body { get; set; }
    public string Category { get; set; } = default!;   // "CaseOverdue" | "ExamClash" | "CaseDueSoon" | ...
    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;
    public string? RefType { get; set; }           // "Case" | "Exam" | "Application"
    public int? RefId { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset? ReadUtc { get; set; }
}
