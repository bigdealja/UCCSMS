using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SMS.Application.Accounting;

namespace SMS.Infrastructure.Accounting;

/// <summary>Posts charges/payments to Sage 300's Web API (Accounts Receivable) over HTTPS,
/// using Basic auth. One of several possible IAccountingProvider implementations — the finance
/// module neither knows nor cares that this is Sage.</summary>
public class Sage300AccountingProvider : IAccountingProvider
{
    private readonly HttpClient _http;
    private readonly Sage300Options _opt;

    public Sage300AccountingProvider(HttpClient http, IOptions<AccountingOptions> opt)
    {
        _opt = opt.Value.Sage300;
        _http = http;
        if (!string.IsNullOrEmpty(_opt.BaseUrl))
        {
            // Enforce HTTPS — the legacy app talked to Sage over plain HTTP; we don't.
            var baseUrl = _opt.BaseUrl.Replace("http://", "https://", StringComparison.OrdinalIgnoreCase).TrimEnd('/') + "/";
            _http.BaseAddress = new Uri(baseUrl);
            var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_opt.Username}:{_opt.Password}"));
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
        }
    }

    public string Name => "Sage300";
    public bool IsExternal => true;

    public async Task<bool> HealthAsync(CancellationToken ct = default)
    {
        if (_http.BaseAddress is null) return false;
        try { var r = await _http.GetAsync($"v1.0/-/{_opt.Company}/AR/ARCustomers?$top=1", ct); return r.IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<AccountingPostResult> PostAsync(AccountingPostRequest request, CancellationToken ct = default)
    {
        if (_http.BaseAddress is null) return AccountingPostResult.Fail("Sage 300 BaseUrl not configured.");
        try
        {
            // Map the generic request to a minimal AR document. Real field mapping is tuned per install;
            // this is the integration seam, intentionally small and swappable.
            var payload = new
            {
                CustomerNumber = request.StudentNumber,
                DocumentType = request.Kind == "Payment" ? "Receipt" : "Invoice",
                DocumentDate = request.Date.ToString("yyyy-MM-dd"),
                Amount = request.Amount,
                DistributionCode = request.DistributionCode ?? request.FeeCode,
                Reference = request.Reference,
                Comment = request.Description
            };
            var body = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync($"v1.0/-/{_opt.Company}/AR/ARInvoiceBatches", body, ct);
            var text = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                return AccountingPostResult.Fail($"Sage 300 returned {(int)resp.StatusCode}: {text}");

            string? extRef = null;
            try { using var doc = JsonDocument.Parse(text);
                  if (doc.RootElement.TryGetProperty("BatchNumber", out var bn)) extRef = bn.ToString(); }
            catch { /* non-JSON ok */ }
            return AccountingPostResult.Ok(extRef);
        }
        catch (Exception ex) { return AccountingPostResult.Fail(ex.Message); }
    }
}
