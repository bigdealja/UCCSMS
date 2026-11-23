using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>A time-limited token giving an external examiner access to their own packages and
/// reports through the examiner portal, without a full staff account.</summary>
public class ExaminerPortalToken : BaseEntity
{
    public int ExaminerId { get; set; }
    public ExternalExaminer Examiner { get; set; } = default!;
    public string Token { get; set; } = default!;
    public DateTimeOffset ExpiresUtc { get; set; }
    public bool Revoked { get; set; }
}
