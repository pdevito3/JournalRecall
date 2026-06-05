using System.Diagnostics.Metrics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using JournalRecall.AI.Core;
using JournalRecall.AI.Observability;
using JournalRecall.AI.Runtime;
using JournalRecall.AI.Tests.Fakes;
using Shouldly;

namespace JournalRecall.AI.Tests.Observability;

public class MetricsTests
{
    private sealed record Measurement(string Instrument, long Value, IReadOnlyDictionary<string, object?> Tags);

    private static MeterListener Listen(List<Measurement> sink)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == Telemetry.MeterName)
                    l.EnableMeasurementEvents(instrument);
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            var dict = new Dictionary<string, object?>();
            foreach (var t in tags)
                dict[t.Key] = t.Value;
            lock (sink)
                sink.Add(new Measurement(instrument.Name, value, dict));
        });
        listener.Start();
        return listener;
    }

    [Fact]
    public async Task Run_emits_run_turn_token_and_tool_metrics()
    {
        var measurements = new List<Measurement>();
        using var listener = Listen(measurements);

        var client = new FakeChatClient()
            .RequestsTool("echo", ToolFakes.TextArg("x"), totalTokens: 7)
            .RespondsWithText("done", totalTokens: 3);
        var runner = RunnerHarness.Build(client, new StubCapabilityResolver([ToolFakes.Echo()]));
        var def = Agent.Define("metered").UsingModel(RunnerHarness.ModelName).WithTool("echo").WithMaxTurns(5).Build();

        await runner.RunAsync(def, Conversation.FromUser("go"), new RunContext());

        var mine = measurements.Where(m => Equals(m.Tags.GetValueOrDefault("agent"), "metered")
            || Equals(m.Tags.GetValueOrDefault("tool"), "echo")).ToList();

        // One completed run, two turns, tokens summed, and one succeeded tool call.
        mine.ShouldContain(m => m.Instrument == "journalrecall.agent.runs"
            && Equals(m.Tags.GetValueOrDefault("outcome"), "completed"));
        mine.Count(m => m.Instrument == "journalrecall.agent.turns").ShouldBe(2);
        mine.Single(m => m.Instrument == "journalrecall.agent.tokens").Value.ShouldBe(10);
        mine.ShouldContain(m => m.Instrument == "journalrecall.agent.tool_calls"
            && Equals(m.Tags.GetValueOrDefault("tool"), "echo")
            && Equals(m.Tags.GetValueOrDefault("status"), "succeeded"));
    }

    [Fact]
    public async Task Unauthorized_tool_records_a_denied_tool_call()
    {
        var measurements = new List<Measurement>();
        using var listener = Listen(measurements);

        var client = new FakeChatClient()
            .RequestsTool("write", ToolFakes.TextArg("x"))
            .RespondsWithText("ok");
        var runner = RunnerHarness.Build(client, new StubCapabilityResolver([
            Microsoft.Extensions.AI.AIFunctionFactory.Create((string text) => text, "write")]));
        var def = Agent.Define("denier").UsingModel(RunnerHarness.ModelName)
            .WithTool("write", scope: "recipes:write").WithMaxTurns(5).Build();

        await runner.RunAsync(def, Conversation.FromUser("go"), new RunContext()); // caller lacks the scope

        measurements.ShouldContain(m => m.Instrument == "journalrecall.agent.tool_calls"
            && Equals(m.Tags.GetValueOrDefault("tool"), "write")
            && Equals(m.Tags.GetValueOrDefault("status"), "denied"));
    }
}
