using JournalRecall.AI.Core;

namespace JournalRecall.AI.Transport;

/// <summary>One conversation message on the wire.</summary>
public sealed record WireMessage(string Role, string Text);

/// <summary>Token usage on the wire.</summary>
public sealed record WireUsage(long? Input, long? Output, long? Total);

/// <summary>
/// The ad-hoc terminal projection of a run: <c>{ stopReason, messages, usage }</c> (ADR-0005).
/// Drained from the event stream for non-streaming callers.
/// </summary>
public sealed record AdHocResponse(string Outcome, string? StopReason, IReadOnlyList<WireMessage> Messages, WireUsage? Usage)
{
    public static AdHocResponse From(AgentOutcome outcome) => outcome switch
    {
        AgentOutcome.Completed c => new("completed", null, Project(c.Messages), Project(c.Usage)),
        AgentOutcome.Stopped s => new("stopped", s.Reason.ToString(), Project(s.Messages), Project(s.Usage)),
        AgentOutcome.Failed f => new("failed", f.Reason, [], null),
        _ => new("unknown", null, [], null),
    };

    private static IReadOnlyList<WireMessage> Project(IReadOnlyList<Microsoft.Extensions.AI.ChatMessage> messages) =>
        messages.Select(m => new WireMessage(m.Role.Value, m.Text)).ToArray();

    private static WireUsage? Project(Microsoft.Extensions.AI.UsageDetails? usage) =>
        usage is null ? null : new WireUsage(usage.InputTokenCount, usage.OutputTokenCount, usage.TotalTokenCount);
}
