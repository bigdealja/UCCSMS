using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>A secure print run for a paper: copies + spares, storage location, dispatch, and the
/// witnessed destruction of unused copies.</summary>
public class PrintRun : BaseEntity
{
    public int QuestionPaperId { get; set; }
    public QuestionPaper QuestionPaper { get; set; } = default!;
    public int Copies { get; set; }
    public int SpareCopies { get; set; }
    public string? Location { get; set; }
    public string? SecureStorageRef { get; set; }
    public DateTimeOffset? DispatchedUtc { get; set; }
    public int ReturnedUnused { get; set; }
    public DateTimeOffset? DestroyedUtc { get; set; }
    public string? DestructionWitness { get; set; }
}
