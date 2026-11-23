using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>A student's result for an exam. Posting results writes the score into the
/// student's Enrollment grade as one undoable batch (same mechanism as imports/Moodle).</summary>
public class ExamResult : BaseEntity
{
    public int ExamId { get; set; }
    public Exam Exam { get; set; } = default!;
    public int StudentId { get; set; }
    public Student Student { get; set; } = default!;
    public double Score { get; set; }
    public bool Posted { get; set; }
    public Guid? PostBatchId { get; set; }
}
