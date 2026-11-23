using SMS.Domain.Common;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities;

/// <summary>An academic term/year (legacy: sms_ADM_D_Term / TermName / AvailableTerms).</summary>
public class Term : BaseEntity
{
    public string Name { get; set; } = default!;
    public int Year { get; set; }
    public TermType Type { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsActive { get; set; }
}
