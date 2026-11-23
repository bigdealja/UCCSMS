using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>Allocates a room (and a number of seats) to an exam. An exam may use several rooms.</summary>
public class ExamRoomAllocation : BaseEntity
{
    public int ExamId { get; set; }
    public Exam Exam { get; set; } = default!;
    public int RoomId { get; set; }
    public Room Room { get; set; } = default!;
    public int SeatsAllocated { get; set; }
}
