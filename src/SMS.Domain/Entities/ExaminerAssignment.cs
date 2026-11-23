using SMS.Domain.Common;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities;

/// <summary>An examiner's assignment to a scope (programme/term), progressing through the 11-stage
/// workflow with a package dispatched to them and a report received back.</summary>
public class ExaminerAssignment : BaseEntity
{
    public int ExaminerId { get; set; }
    public ExternalExaminer Examiner { get; set; } = default!;
    public int? ProgrammeId { get; set; }
    public Programme? Programme { get; set; }
    public int? TermId { get; set; }
    public Term? Term { get; set; }

    public AssignmentStage Stage { get; set; } = AssignmentStage.Nominated;
    public DateTimeOffset? PackageDispatchedUtc { get; set; }
    public string? Notes { get; set; }

    public ICollection<ExaminerReport> Reports { get; set; } = new List<ExaminerReport>();
}
