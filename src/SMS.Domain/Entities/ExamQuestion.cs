using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>A question placed on an exam, with its order and mark weight for that exam.</summary>
public class ExamQuestion : BaseEntity
{
    public int ExamId { get; set; }
    public Exam Exam { get; set; } = default!;
    public int QuestionId { get; set; }
    public Question Question { get; set; } = default!;
    public int Order { get; set; }
    public double Marks { get; set; }
}
