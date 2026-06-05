namespace JournalRecall.AI.Transport;

/// <summary>Streaming transport for agent endpoints (ADR-0005). SSE is the default.</summary>
public enum StreamTransport
{
    /// <summary>Server-Sent Events (default).</summary>
    Sse,

    /// <summary>Newline-delimited JSON.</summary>
    Ndjson,

    /// <summary>No streaming; ad-hoc terminal projection only.</summary>
    None,
}

/// <summary>Configured streaming options.</summary>
public sealed class StreamingOptions
{
    public StreamTransport Transport { get; set; } = StreamTransport.Sse;
}
