using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>Audit log of every assistant question and answer (data-protection: who asked what).</summary>
public class AssistantQueryLog : BaseEntity
{
    public string? AskedBy { get; set; }
    public int? StudentId { get; set; }
    public string Question { get; set; } = default!;
    public string? Answer { get; set; }
    public bool UsedAi { get; set; }
}
