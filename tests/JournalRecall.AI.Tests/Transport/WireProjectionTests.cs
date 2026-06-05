using Shouldly;
using JournalRecall.AI.Core;
using JournalRecall.AI.Transport;

namespace JournalRecall.AI.Tests.Transport;

public class WireProjectionTests
{
    private static readonly DateTimeOffset Ts = DateTimeOffset.UnixEpoch;

    [Fact]
    public void Envelope_carries_version_seq_and_timestamp()
    {
        var envelope = WireProjection.ToEnvelope(new AgentEvent.TurnStarted(2), seq: 5, Ts);

        envelope.V.ShouldBe(WireEnvelope.Version);
        envelope.Type.ShouldBe("turn.started");
        envelope.Seq.ShouldBe(5);
        envelope.Ts.ShouldBe(Ts);
    }

    [Theory]
    [InlineData("run.started")]
    [InlineData("tool.invoking")]
    [InlineData("completed")]
    public void Event_types_map_to_stable_discriminators(string expectedType)
    {
        AgentEvent @event = expectedType switch
        {
            "run.started" => new AgentEvent.RunStarted("a", "corr"),
            "tool.invoking" => new AgentEvent.ToolInvoking("t", "c1"),
            _ => new AgentEvent.Completed(new AgentOutcome.Completed([])),
        };

        WireProjection.ToEnvelope(@event, 0, Ts).Type.ShouldBe(expectedType);
    }

    [Fact]
    public void Terminal_events_project_the_adhoc_response()
    {
        var outcome = new AgentOutcome.Stopped(StopReason.MaxTurns, []);
        var envelope = WireProjection.ToEnvelope(new AgentEvent.Stopped(outcome), 0, Ts);

        envelope.Type.ShouldBe("stopped");
        envelope.Data.ShouldBeOfType<AdHocResponse>()
            .StopReason.ShouldBe("MaxTurns");
    }
}
