using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SMS.Application.Crm;

namespace SMS.Infrastructure.Crm;

/// <summary>Pushes leads to any external system via a configured HTTPS webhook (POST JSON).
/// Vendor-neutral: point it at another CRM's inbound webhook, an automation platform, or a
/// custom endpoint. Replaces the need for a specific marketing tool like Mautic.</summary>
public class WebhookCrmProvider : ICrmProvider
{
    private readonly HttpClient _http;
    private readonly WebhookOptions _opt;
    public WebhookCrmProvider(HttpClient http, IOptions<CrmOptions> opt)
    {
        _opt = opt.Value.Webhook;
        _http = http;
        if (!string.IsNullOrEmpty(_opt.AuthHeaderName) && !string.IsNullOrEmpty(_opt.AuthHeaderValue))
            _http.DefaultRequestHeaders.TryAddWithoutValidation(_opt.AuthHeaderName, _opt.AuthHeaderValue);
    }
    public string Name => "Webhook";
    public bool IsExternal => true;

    public Task<bool> HealthAsync(CancellationToken ct = default)
        => Task.FromResult(!string.IsNullOrEmpty(_opt.Url) && Uri.TryCreate(_opt.Url, UriKind.Absolute, out _));

    public async Task<CrmUpsertResult> UpsertLeadAsync(CrmLead lead, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_opt.Url)) return new CrmUpsertResult(false, null, "Webhook URL not configured.");
        try
        {
            var body = new StringContent(JsonSerializer.Serialize(lead), Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync(_opt.Url, body, ct);
            return resp.IsSuccessStatusCode
                ? new CrmUpsertResult(true, null, null)
                : new CrmUpsertResult(false, null, $"Webhook returned {(int)resp.StatusCode}.");
        }
        catch (Exception ex) { return new CrmUpsertResult(false, null, ex.Message); }
    }
}
