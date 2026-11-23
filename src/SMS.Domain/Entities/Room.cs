using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>An exam venue/room on a campus, with a seating capacity used for allocation and
/// capacity clash checks.</summary>
public class Room : BaseEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public int CampusId { get; set; }
    public Campus Campus { get; set; } = default!;
    public int Capacity { get; set; }
    public bool IsActive { get; set; } = true;
}
