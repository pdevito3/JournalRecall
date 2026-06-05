using JournalRecall.AI.OpenAI;

namespace JournalRecall.Api.Domain.Admin;

/// <summary>
/// The app-wide AI provider configuration (issue 0016, ADR-0002): a single BYO OpenAI-compatible (or
/// Azure OpenAI) endpoint + model that the Cleanup and Summary features consume. One row for the whole
/// installation — not per-user — set only by an Admin. When unset, the features fall back to the
/// appsettings <c>ChatModels:*</c> defaults.
/// </summary>
public sealed class AiProviderSettings : BaseEntity
{
    public ChatProvider Provider { get; private set; } = ChatProvider.OpenAI;
    public string? Endpoint { get; private set; }
    public string? ApiKey { get; private set; }
    public string Model { get; private set; } = string.Empty;

    private AiProviderSettings() { } // EF

    public static AiProviderSettings Create() => new();

    public void Update(ChatProvider provider, string? endpoint, string? apiKey, string model)
    {
        Provider = provider;
        Endpoint = string.IsNullOrWhiteSpace(endpoint) ? null : endpoint.Trim();
        ApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        Model = (model ?? string.Empty).Trim();
    }

    /// <summary>True once a model is configured — the cue to use this over the appsettings fallback.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Model);
}
