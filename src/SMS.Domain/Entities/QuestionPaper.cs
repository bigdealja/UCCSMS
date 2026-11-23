using SMS.Domain.Common;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities;

/// <summary>An expected exam question paper for a course sitting, tracked from submission through
/// vetting/approval, printing, secure dispatch, and return/destruction.</summary>
public class QuestionPaper : BaseEntity
{
    public string Title { get; set; } = default!;
    public int CourseId { get; set; }
    public Course Course { get; set; } = default!;
    public int TermId { get; set; }
    public Term Term { get; set; } = default!;

    public DateOnly SubmissionDeadline { get; set; }
    public string? AuthorLecturer { get; set; }
    public bool ExternalReviewRequired { get; set; }

    public PaperStatus Status { get; set; } = PaperStatus.Expected;
    public int CurrentVersion { get; set; }
    public DateTimeOffset? ReturnedUtc { get; set; }
    public string? WithdrawnReason { get; set; }

    public ICollection<QuestionPaperVersion> Versions { get; set; } = new List<QuestionPaperVersion>();
    public ICollection<PrintRun> PrintRuns { get; set; } = new List<PrintRun>();
}
