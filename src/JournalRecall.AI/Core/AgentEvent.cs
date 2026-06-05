using Microsoft.Extensions.AI;

namespace JournalRecall.AI.Core;

/// <summary>
/// One typed lifecycle event yielded by the runner. A single stream powers internal OTel/logging
/// subscribers, outward progress notifications, and the optional wire transports (ADR-0005). This is
/// the rich <i>internal</i> union; transports project it to a stable versioned wire envelope.
/// </summary>
public abstract record AgentEvent
{
    private AgentEvent() { }

    /// <summary>The run has started.</summary>
    public sealed record RunStarted(string AgentName, string CorrelationId) : AgentEvent;

    /// <summary>A model turn has begun.</summary>
    public sealed record TurnStarted(int Turn) : AgentEvent;

    /// <summary>A streamed fragment of model output.</summary>
    public sealed record ModelDelta(string Text) : AgentEvent;

    /// <summary>A tool is about to be invoked.</summary>
    public sealed record ToolInvoking(string ToolName, string? CallId = null) : AgentEvent;

    /// <summary>A tool invocation succeeded.</summary>
    public sealed record ToolSucceeded(string ToolName, string? CallId = null) : AgentEvent;

    /// <summary>A tool invocation failed.</summary>
    public sealed record ToolFailed(string ToolName, string Error, string? CallId = null) : AgentEvent;

    /// <summary>A resource was read into context.</summary>
    public sealed record ResourceRead(string ResourceName) : AgentEvent;

    /// <summary>
    /// A progress notification (e.g. forwarded from a long-running MCP tool). <see cref="Value"/> and
    /// optional <see cref="Total"/> describe completion; <see cref="Message"/> is human-facing.
    /// </summary>
    public sealed record Progress(double Value, double? Total = null, string? Message = null) : AgentEvent;

    /// <summary>The agent delegated to a sub-agent.</summary>
    public sealed record AgentDelegated(string AgentName) : AgentEvent;

    /// <summary>Cumulative token usage was updated.</summary>
    public sealed record UsageUpdated(long TotalTokens, UsageDetails? Details = null) : AgentEvent;

    /// <summary>Terminal: the run completed naturally.</summary>
    public sealed record Completed(AgentOutcome.Completed Outcome) : AgentEvent;

    /// <summary>Terminal: the run was stopped by a guardrail.</summary>
    public sealed record Stopped(AgentOutcome.Stopped Outcome) : AgentEvent;

    /// <summary>Terminal: the run failed.</summary>
    public sealed record Failed(AgentOutcome.Failed Outcome) : AgentEvent;
}
