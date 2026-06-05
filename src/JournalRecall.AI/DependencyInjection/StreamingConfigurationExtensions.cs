using Microsoft.Extensions.DependencyInjection;
using JournalRecall.AI.Transport;

namespace JournalRecall.AI.DependencyInjection;

/// <summary>Configures the streaming transport (ADR-0005).</summary>
public static class StreamingConfigurationExtensions
{
    /// <summary>Selects the streaming transport (SSE default, NDJSON, or None).</summary>
    public static IJournalRecallAgentsBuilder WithStreaming(
        this IJournalRecallAgentsBuilder builder, StreamTransport transport = StreamTransport.Sse)
    {
        builder.Services.Configure<StreamingOptions>(o => o.Transport = transport);
        return builder;
    }
}
