namespace JournalRecall.AI.OpenAI;

/// <summary>Which OpenAI-shaped provider backs a logical chat model.</summary>
public enum ChatProvider
{
    /// <summary>OpenAI or any OpenAI-compatible endpoint (Ollama, vLLM, LM Studio, Groq, Together, OpenRouter, ...).</summary>
    OpenAI,

    /// <summary>Azure OpenAI (deployment-name semantics, api-version, AzureOpenAIClient).</summary>
    AzureOpenAI,
}

/// <summary>Configuration for a single logical chat model (bound from <c>ChatModels:{key}</c>).</summary>
public sealed class ChatModelOptions
{
    /// <summary>Provider family. Defaults to OpenAI-compatible.</summary>
    public ChatProvider Provider { get; set; } = ChatProvider.OpenAI;

    /// <summary>
    /// Base endpoint. Required for Azure and for OpenAI-compatible providers (e.g. Ollama). Leave null
    /// for OpenAI proper to use the SDK default (api.openai.com).
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>API key. Optional for keyless local providers; required for OpenAI and Azure.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Model id (OpenAI-compatible) or deployment name (Azure OpenAI).</summary>
    public string Model { get; set; } = "";
}
