using SMS.Application.Ai;

namespace SMS.Infrastructure.Ai;

/// <summary>Default: AI disabled. Features that use it fall back to deterministic rules.</summary>
public class NullAiAssistant : IAiAssistant
{
    public string Name => "None";
    public bool IsConfigured => false;
    public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
        => Task.FromResult(string.Empty);
}
