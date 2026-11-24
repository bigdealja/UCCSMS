using SMS.Domain.Common;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities;

/// <summary>A student's on-campus housing assignment (hall + room) for a term.</summary>
public class HousingAssignment : BaseEntity
{
    public int StudentId { get; set; }
    public Student Student { get; set; } = default!;
    public string Hall { get; set; } = default!;
    public string Room { get; set; } = default!;
    public int? TermId { get; set; }
    public Term? Term { get; set; }
    public HousingStatus Status { get; set; } = HousingStatus.Assigned;
    public DateTimeOffset? CheckedInUtc { get; set; }
    public DateTimeOffset? CheckedOutUtc { get; set; }
}
