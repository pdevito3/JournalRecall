using Microsoft.Extensions.AI;

namespace JournalRecall.AI.Core;

/// <summary>
/// Why a run stopped short of natural completion. Expected conditions are values, never exceptions
/// (ADR-0006).
/// </summary>
public enum StopReason
{
    MaxTurns,
    Duration,
    Budget,
    Policy,
    Cancelled,
}

/// <summary>
/// The terminal result of an agent run. A pure value produced by the shell and consumed by callers.
/// <c>Completed | Stopped | Failed</c> (CONTEXT.md, ADR-0006).
/// </summary>
public abstract record AgentOutcome
{
    private AgentOutcome() { }

    /// <summary>The run finished naturally with a final model response.</summary>
    public sealed record Completed(IReadOnlyList<ChatMessage> Messages, UsageDetails? Usage = null)
        : AgentOutcome;

    /// <summary>A per-run guardrail (turns / duration / budget / policy / cancellation) ended the run.</summary>
    public sealed record Stopped(
        StopReason Reason,
        IReadOnlyList<ChatMessage> Messages,
        UsageDetails? Usage = null) : AgentOutcome;

    /// <summary>An unrecoverable failure ended the run.</summary>
    public sealed record Failed(string Reason, Exception? Exception = null) : AgentOutcome;
}
