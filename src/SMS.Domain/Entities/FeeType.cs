using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>A catalogue of charge types (tuition, lab fee, housing, …). DistributionCode maps
/// to the external accounting system's revenue/GL code (e.g. Sage item/distribution code).</summary>
public class FeeType : BaseEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public decimal DefaultAmount { get; set; }
    public string? DistributionCode { get; set; }   // accounting GL/revenue code
    public bool IsActive { get; set; } = true;
}
