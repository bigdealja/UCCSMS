using SMS.Domain.Common;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities;

/// <summary>An external examiner: register entry with appointment, contract and conflict declaration.</summary>
public class ExternalExaminer : BaseEntity
{
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string? Email { get; set; }
    public string? Institution { get; set; }
    public ExaminerStatus Status { get; set; } = ExaminerStatus.Prospective;
    public DateOnly? AppointmentStart { get; set; }
    public DateOnly? AppointmentEnd { get; set; }
    public string? ContractReference { get; set; }

    // Conflict of interest declaration
    public bool ConflictDeclarationRecorded { get; set; }
    public bool HasConflict { get; set; }
    public string? ConflictDetails { get; set; }

    public ICollection<ExaminerAssignment> Assignments { get; set; } = new List<ExaminerAssignment>();
}
