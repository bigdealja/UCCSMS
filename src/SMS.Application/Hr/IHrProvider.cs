namespace SMS.Application.Hr;

/// <summary>A completed-work payment to export to an HR/payroll system. Vendor-neutral.</summary>
public record HrExportRequest(string Claimant, string WorkType, decimal Units, decimal Amount, string? Reference, DateOnly Date);
public record HrExportResult(bool Success, string? ExternalRef, string? Error)
{
    public static HrExportResult Ok(string? r) => new(true, r, null);
    public static HrExportResult Fail(string e) => new(false, null, e);
}

/// <summary>Pluggable HR/payroll integration — built-in (None) or an external HR system. The payroll
/// module depends only on this interface, same pattern as accounting and CRM.</summary>
public interface IHrProvider
{
    string Name { get; }
    bool IsExternal { get; }
    Task<bool> HealthAsync(CancellationToken ct = default);
    Task<HrExportResult> ExportAsync(HrExportRequest request, CancellationToken ct = default);
}

public class HrOptions
{
    public string Provider { get; set; } = "None";   // "None" | "Webhook"
    public HrWebhookOptions Webhook { get; set; } = new();
}
public class HrWebhookOptions
{
    public string Url { get; set; } = "";
    public string? AuthHeaderName { get; set; }
    public string? AuthHeaderValue { get; set; }
}
