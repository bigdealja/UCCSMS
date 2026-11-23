using SMS.Domain.Common;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities;

/// <summary>A scheduled examination for a course in a term. Ties the Examinations module
/// to the academic model; results post back to Enrollment grades (and on to Moodle).</summary>
public class Exam : BaseEntity
{
    public string Title { get; set; } = default!;
    public int CourseId { get; set; }
    public Course Course { get; set; } = default!;
    public int TermId { get; set; }
    public Term Term { get; set; } = default!;

    public DateTimeOffset? ScheduledStart { get; set; }
    public int DurationMinutes { get; set; }
    public string? Room { get; set; }
    public double TotalMarks { get; set; }
    public ExamStatus Status { get; set; } = ExamStatus.Draft;

    public ICollection<ExamQuestion> Questions { get; set; } = new List<ExamQuestion>();
    public ICollection<ExamResult> Results { get; set; } = new List<ExamResult>();
}
