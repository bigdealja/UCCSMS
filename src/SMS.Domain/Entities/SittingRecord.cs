using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>One operational record per exam sitting. Milestone timestamps drive an automatically
/// computed stage and status (ready / at-risk / overdue / blocked / complete).</summary>
public class SittingRecord : BaseEntity
{
    public int ExamId { get; set; }
    public Exam Exam { get; set; } = default!;
    public DateOnly DueDate { get; set; }

    public DateTimeOffset? ScriptsCollectedUtc { get; set; }
    public DateTimeOffset? MarkedUtc { get; set; }
    public DateTimeOffset? GradeSheetReturnedUtc { get; set; }
    public DateTimeOffset? EnteredUtc { get; set; }
    public DateTimeOffset? SentToDeanUtc { get; set; }
    public DateTimeOffset? VettedUtc { get; set; }
    public DateTimeOffset? PublishedUtc { get; set; }

    public string? ExceptionNote { get; set; }   // blocks the sitting until cleared
    public string? LateReason { get; set; }
}
