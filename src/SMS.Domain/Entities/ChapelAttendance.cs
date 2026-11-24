using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>A chapel/assembly attendance record for a student at an event.</summary>
public class ChapelAttendance : BaseEntity
{
    public int StudentId { get; set; }
    public Student Student { get; set; } = default!;
    public string EventName { get; set; } = default!;
    public DateOnly EventDate { get; set; }
    public bool Present { get; set; }
}
