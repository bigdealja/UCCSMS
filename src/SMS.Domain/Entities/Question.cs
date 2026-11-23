using SMS.Domain.Common;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities;

/// <summary>A reusable question in a course's question bank.</summary>
public class Question : BaseEntity
{
    public int CourseId { get; set; }
    public Course Course { get; set; } = default!;
    public QuestionType Type { get; set; }
    public string Text { get; set; } = default!;
    public double DefaultMarks { get; set; } = 1;

    /// <summary>JSON array of options for MultipleChoice/TrueFalse (null for open questions).</summary>
    public string? OptionsJson { get; set; }
    /// <summary>Correct answer (option index/text) for auto-markable types.</summary>
    public string? CorrectAnswer { get; set; }
}
