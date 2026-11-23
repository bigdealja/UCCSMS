namespace SMS.Application.Ai;

/// <summary>Pluggable AI backend. Default is disabled (None). A configured provider (an
/// OpenAI-compatible endpoint — works with OpenAI, Azure OpenAI, or a self-hosted model gateway;
/// other providers such as Anthropic can be added as further implementations) powers advisory
/// features. AI NEVER decides — it returns recommendations a human approves.</summary>
public interface IAiAssistant
{
    string Name { get; }
    bool IsConfigured { get; }
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}

public class AiOptions
{
    public string Provider { get; set; } = "None";   // "None" | "OpenAiCompatible"
    /// <summary>Master switch — AI only runs when explicitly enabled (data-protection approval).</summary>
    public bool Enabled { get; set; } = false;
    public string BaseUrl { get; set; } = "";         // e.g. https://api.openai.com/v1 or a self-hosted gateway
    public string ApiKey { get; set; } = "";          // supplied via env/secret
    public string Model { get; set; } = "";            // e.g. the model id your endpoint serves
    public double Temperature { get; set; } = 0.0;      // deterministic by default for decisions
}
