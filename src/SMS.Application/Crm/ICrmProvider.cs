namespace SMS.Application.Crm;

/// <summary>A lead to mirror to an external system. Generic — no vendor concepts.</summary>
public record CrmLead(string FirstName, string LastName, string? Email, string? Phone, string? Source, string? Stage);
public record CrmUpsertResult(bool Success, string? ExternalId, string? Error);

/// <summary>Pluggable CRM/lead integration. Default is the built-in CRM (leads live in our own
/// database). An optional generic webhook can push leads to ANY external system (another CRM,
/// an automation tool, a custom endpoint) without vendor-specific code. Named vendor providers
/// (Salesforce, HubSpot, …) can be added later as further implementations.</summary>
public interface ICrmProvider
{
    string Name { get; }
    bool IsExternal { get; }
    Task<bool> HealthAsync(CancellationToken ct = default);
    Task<CrmUpsertResult> UpsertLeadAsync(CrmLead lead, CancellationToken ct = default);
}

public class CrmOptions
{
    public string Provider { get; set; } = "BuiltIn";   // "BuiltIn" | "Webhook" | (future) "Salesforce" | "HubSpot"
    public WebhookOptions Webhook { get; set; } = new();
    /// <summary>Optional shared secret the public website must send (X-Capture-Secret) to post leads.</summary>
    public string? CaptureSecret { get; set; }
}

/// <summary>Config for the generic outbound webhook provider.</summary>
public class WebhookOptions
{
    public string Url { get; set; } = "";          // HTTPS endpoint to POST lead JSON to
    public string? AuthHeaderName { get; set; }     // optional, e.g. "Authorization" or "X-Api-Key"
    public string? AuthHeaderValue { get; set; }    // secret, supplied via env
}
