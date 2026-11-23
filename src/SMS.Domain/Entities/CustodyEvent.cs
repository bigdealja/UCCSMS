using SMS.Domain.Common;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities;

/// <summary>An entry in a script bundle's chain of custody (append-only).</summary>
public class CustodyEvent : BaseEntity
{
    public int ScriptBundleId { get; set; }
    public ScriptBundle ScriptBundle { get; set; } = default!;
    public CustodyAction Action { get; set; }
    public string? FromCustodian { get; set; }
    public string? ToCustodian { get; set; }
    public string? By { get; set; }
    public string? Notes { get; set; }
}
