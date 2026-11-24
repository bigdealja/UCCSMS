using SMS.Application.Hr;
namespace SMS.Infrastructure.Hr;

/// <summary>Default: no external HR system. Approved claims are recorded; nothing is exported.</summary>
public class NullHrProvider : IHrProvider
{
    public string Name => "None";
    public bool IsExternal => false;
    public Task<bool> HealthAsync(CancellationToken ct = default) => Task.FromResult(true);
    public Task<HrExportResult> ExportAsync(HrExportRequest r, CancellationToken ct = default) => Task.FromResult(HrExportResult.Ok(null));
}
