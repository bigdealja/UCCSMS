using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>A policy/FAQ article the Q&A assistant grounds its answers in (the EMS brief's
/// versioned policy library). Managed by staff; the assistant never answers outside this
/// knowledge plus the asker's own records.</summary>
public class KnowledgeArticle : BaseEntity
{
    public string Title { get; set; } = default!;
    public string Category { get; set; } = default!;   // "Grading" | "Admissions" | "Exams" | "Finance" | "Cases"
    public string Body { get; set; } = default!;
    public string? Tags { get; set; }                   // space/comma separated keywords
    public bool IsPublished { get; set; } = true;
}
