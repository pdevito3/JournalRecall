namespace JournalRecall.AI.Core.Persistence;

/// <summary>
/// A durable record of one agent-activity item from a turn (tool call, resource read, sub-agent
/// delegation, or progress note) — the persisted projection of the non-message
/// <see cref="AgentEvent"/>s, so a past turn's activity can be replayed in the UI (ADR-0007).
/// Tool entries are folded to their final state (invoking → succeeded/failed), mirroring the live
/// transcript reducer.
/// </summary>
public sealed record StoredActivity
{
    /// <summary>"tool" | "resource" | "delegate" | "progress".</summary>
    public required string Kind { get; init; }

    // tool
    public string? Tool { get; init; }
    public string? Status { get; init; } // "invoking" | "succeeded" | "failed"
    public string? CallId { get; init; }
    public string? Error { get; init; }

    // resource
    public string? Resource { get; init; }

    // delegate
    public string? Agent { get; init; }

    // progress
    public double? Value { get; init; }
    public double? Total { get; init; }
    public string? Message { get; init; }

    /// <summary>When this item occurred (for a tool, when it started).</summary>
    public DateTimeOffset? OccurredAt { get; init; }

    /// <summary>For a tool, the elapsed time from invoking to its success/failure, in milliseconds.</summary>
    public double? DurationMs { get; init; }
}
