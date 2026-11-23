using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SMS.Application.Ai;

namespace SMS.Infrastructure.Ai;

/// <summary>Calls an OpenAI-compatible chat-completions endpoint (OpenAI, Azure OpenAI, or a
/// self-hosted model gateway) over HTTPS. Configured and enabled by Ai:* settings; the API key
/// comes from a secret, never source. Used only for advisory output that a human approves.</summary>
public class ChatCompletionAiAssistant : IAiAssistant
{
    private readonly HttpClient _http;
    private readonly AiOptions _opt;
    public ChatCompletionAiAssistant(HttpClient http, IOptions<AiOptions> opt)
    {
        _opt = opt.Value;
        _http = http;
        if (!string.IsNullOrEmpty(_opt.BaseUrl))
        {
            _http.BaseAddress = new Uri(_opt.BaseUrl.TrimEnd('/') + "/");
            if (!string.IsNullOrEmpty(_opt.ApiKey))
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _opt.ApiKey);
        }
    }
    public string Name => "OpenAiCompatible";
    public bool IsConfigured => _opt.Enabled && !string.IsNullOrEmpty(_opt.BaseUrl) && !string.IsNullOrEmpty(_opt.Model);

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        if (!IsConfigured) return string.Empty;
        var payload = new
        {
            model = _opt.Model,
            temperature = _opt.Temperature,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };
        var body = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync("chat/completions", body, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }
}
