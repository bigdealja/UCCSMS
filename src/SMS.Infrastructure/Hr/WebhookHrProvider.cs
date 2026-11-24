using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SMS.Application.Hr;

namespace SMS.Infrastructure.Hr;

/// <summary>Exports approved claims to any HR/payroll system via a configured HTTPS webhook.</summary>
public class WebhookHrProvider : IHrProvider
{
    private readonly HttpClient _http;
    private readonly HrWebhookOptions _opt;
    public WebhookHrProvider(HttpClient http, IOptions<HrOptions> opt)
    {
        _opt = opt.Value.Webhook; _http = http;
        if (!string.IsNullOrEmpty(_opt.AuthHeaderName) && !string.IsNullOrEmpty(_opt.AuthHeaderValue))
            _http.DefaultRequestHeaders.TryAddWithoutValidation(_opt.AuthHeaderName, _opt.AuthHeaderValue);
    }
    public string Name => "Webhook";
    public bool IsExternal => true;
    public Task<bool> HealthAsync(CancellationToken ct = default)
        => Task.FromResult(!string.IsNullOrEmpty(_opt.Url) && Uri.TryCreate(_opt.Url, UriKind.Absolute, out _));

    public async Task<HrExportResult> ExportAsync(HrExportRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_opt.Url)) return HrExportResult.Fail("HR webhook URL not configured.");
        try
        {
            var body = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync(_opt.Url, body, ct);
            return resp.IsSuccessStatusCode ? HrExportResult.Ok(null) : HrExportResult.Fail($"HR webhook {(int)resp.StatusCode}.");
        }
        catch (Exception ex) { return HrExportResult.Fail(ex.Message); }
    }
}
