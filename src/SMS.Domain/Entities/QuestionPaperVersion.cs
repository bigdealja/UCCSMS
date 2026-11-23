using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>A submitted version of a question paper (links to the stored file in the Documents module).</summary>
public class QuestionPaperVersion : BaseEntity
{
    public int QuestionPaperId { get; set; }
    public QuestionPaper QuestionPaper { get; set; } = default!;
    public int VersionNumber { get; set; }
    public string FileName { get; set; } = default!;
    public int? DocumentId { get; set; }
    public string? SubmittedBy { get; set; }
    public string? Notes { get; set; }
}
