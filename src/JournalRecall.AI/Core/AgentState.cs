using Microsoft.Extensions.AI;

namespace JournalRecall.AI.Core;

/// <summary>
/// The pure loop state the policy core folds over between turns (messages, turn count, tokens,
/// retries). Immutable: each fold produces a new value, making the policy trivially testable
/// (ADR-0001). The shell stamps <see cref="Now"/> with the current clock reading each turn so the
/// pure <see cref="AgentPolicy.Decide"/> stays free of ambient time.
/// </summary>
public sealed record AgentState
{
    public required AgentDefinition Definition { get; init; }
    public required RunContext Context { get; init; }

    /// <summary>Number of completed model turns.</summary>
    public int Turn { get; init; }

    /// <summary>Total tokens consumed so far (input + output).</summary>
    public long TokensUsed { get; init; }

    /// <summary>The full message list sent to the model (leading context + conversation).</summary>
    public IReadOnlyList<ChatMessage> Messages { get; init; } = [];

    /// <summary>
    /// Count of leading system/pinned/prompt context messages to exclude from the reported
    /// conversation transcript (they are run-scoped, not conversation history).
    /// </summary>
    public int ContextMessageCount { get; init; }

    /// <summary>When the run started (wall clock).</summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>The shell's current clock reading, stamped each turn for deadline evaluation.</summary>
    public DateTimeOffset Now { get; init; }

    /// <summary>Per-tool feed-back-retry counts consumed by <see cref="AgentPolicy.OnToolError"/>.</summary>
    public IReadOnlyDictionary<string, int> ToolRetries { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>True once the caller has requested cancellation.</summary>
    public bool Cancelled { get; init; }

    /// <summary>True when the latest model response is a final answer with no pending tool calls.</summary>
    public bool ModelProducedFinalResponse { get; init; }

    /// <summary>The effective deadline = the earliest of the context deadline and start + definition max duration.</summary>
    public DateTimeOffset? EffectiveDeadline
    {
        get
        {
            DateTimeOffset? fromDuration =
                Definition.MaxDuration is { } d ? StartedAt + d : null;
            return (Context.Deadline, fromDuration) switch
            {
                (null, null) => null,
                ({ } a, null) => a,
                (null, { } b) => b,
                ({ } a, { } b) => a < b ? a : b,
            };
        }
    }

    /// <summary>Retry count recorded for a given tool name.</summary>
    public int RetriesFor(string toolName) =>
        ToolRetries.TryGetValue(toolName, out var n) ? n : 0;

    /// <summary>The conversation transcript (excludes leading run-scoped context).</summary>
    public IReadOnlyList<ChatMessage> Transcript =>
        ContextMessageCount <= 0 ? Messages : Messages.Skip(ContextMessageCount).ToArray();
}
