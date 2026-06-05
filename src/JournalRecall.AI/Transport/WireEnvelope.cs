using JournalRecall.AI.Core;

namespace JournalRecall.AI.Transport;

/// <summary>
/// The stable, versioned public wire envelope a transport projects each <see cref="AgentEvent"/> into
/// (ADR-0005). Internal events evolve freely; this contract stays stable.
/// </summary>
public sealed record WireEnvelope(int V, string Type, long Seq, DateTimeOffset Ts, object? Data)
{
    public const int Version = 1;
}

/// <summary>Projects the rich internal <see cref="AgentEvent"/> union onto the public wire contract.</summary>
public static class WireProjection
{
    public static WireEnvelope ToEnvelope(AgentEvent @event, long seq, DateTimeOffset timestamp)
    {
        var (type, data) = Map(@event);
        return new WireEnvelope(WireEnvelope.Version, type, seq, timestamp, data);
    }

    private static (string Type, object? Data) Map(AgentEvent @event) => @event switch
    {
        AgentEvent.RunStarted e => ("run.started", new { agent = e.AgentName, correlationId = e.CorrelationId }),
        AgentEvent.TurnStarted e => ("turn.started", new { turn = e.Turn }),
        AgentEvent.ModelDelta e => ("model.delta", new { text = e.Text }),
        AgentEvent.ToolInvoking e => ("tool.invoking", new { tool = e.ToolName, callId = e.CallId }),
        AgentEvent.ToolSucceeded e => ("tool.succeeded", new { tool = e.ToolName, callId = e.CallId }),
        AgentEvent.ToolFailed e => ("tool.failed", new { tool = e.ToolName, error = e.Error, callId = e.CallId }),
        AgentEvent.ResourceRead e => ("resource.read", new { resource = e.ResourceName }),
        AgentEvent.Progress e => ("progress", new { value = e.Value, total = e.Total, message = e.Message }),
        AgentEvent.AgentDelegated e => ("agent.delegated", new { agent = e.AgentName }),
        AgentEvent.UsageUpdated e => ("usage.updated", new { totalTokens = e.TotalTokens }),
        AgentEvent.Completed e => ("completed", AdHocResponse.From(e.Outcome)),
        AgentEvent.Stopped e => ("stopped", AdHocResponse.From(e.Outcome)),
        AgentEvent.Failed e => ("failed", AdHocResponse.From(e.Outcome)),
        _ => ("unknown", null),
    };
}
