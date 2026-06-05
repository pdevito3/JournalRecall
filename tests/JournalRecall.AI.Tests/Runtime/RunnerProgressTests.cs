using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using JournalRecall.AI.Core;
using JournalRecall.AI.Tests.Fakes;
using Shouldly;

namespace JournalRecall.AI.Tests.Runtime;

/// <summary>
/// The runner drains the per-run progress queue onto the event stream. Uses a tool that enqueues
/// progress synchronously during its invocation, so the assertion is deterministic (no async MCP
/// delivery timing).
/// </summary>
public class RunnerProgressTests
{
    [Fact]
    public async Task Progress_queued_during_a_tool_call_is_emitted_on_the_stream()
    {
        var progress = new ConcurrentQueue<AgentEvent>();
        var tool = AIFunctionFactory.Create(() =>
        {
            progress.Enqueue(new AgentEvent.Progress(42, 100, "working"));
            return "ok";
        }, "work");

        var client = new FakeChatClient().RequestsTool("work").RespondsWithText("done");
        var runner = RunnerHarness.Build(client, new StubCapabilityResolver([tool], progress: progress));
        var def = Agent.Define("a").UsingModel(RunnerHarness.ModelName).WithTool("work").WithMaxTurns(5).Build();

        var events = new List<AgentEvent>();
        await foreach (var e in runner.StreamAsync(def, Conversation.FromUser("go"), new RunContext()))
            events.Add(e);

        events.OfType<AgentEvent.Progress>()
            .ShouldContain(p => p.Value == 42 && p.Total == 100 && p.Message == "working");
        events[^1].ShouldBeOfType<AgentEvent.Completed>();
    }
}
