using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>A student's participation in a sport/team for a season.</summary>
public class SportsParticipation : BaseEntity
{
    public int StudentId { get; set; }
    public Student Student { get; set; } = default!;
    public string Sport { get; set; } = default!;
    public string? Team { get; set; }
    public string? Role { get; set; }     // "Player" | "Captain" | …
    public string Season { get; set; } = default!;
    public bool Active { get; set; } = true;
}
