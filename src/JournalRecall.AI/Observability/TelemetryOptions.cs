namespace JournalRecall.AI.Observability;

/// <summary>
/// Telemetry content policy (ADR-0005). Metadata (model, tokens, tool names, latency, finish reason,
/// error type) is always captured; prompt/response <b>content</b> capture is opt-in per environment
/// and passes through a pluggable <see cref="Redactor"/> before export. Safe-by-default for a
/// health-adjacent domain.
/// </summary>
public sealed class TelemetryOptions
{
    /// <summary>When false (the default), prompt/response content is never placed on spans or logs.</summary>
    public bool CaptureContent { get; set; }

    /// <summary>Applied to any captured content before export. Defaults to a no-op.</summary>
    public ITelemetryRedactor Redactor { get; set; } = NullRedactor.Instance;
}

/// <summary>Transforms captured content (e.g. masks PII) before it reaches telemetry exporters.</summary>
public interface ITelemetryRedactor
{
    string Redact(string content);
}

/// <summary>A redactor that passes content through unchanged.</summary>
public sealed class NullRedactor : ITelemetryRedactor
{
    public static readonly NullRedactor Instance = new();
    public string Redact(string content) => content;
}
