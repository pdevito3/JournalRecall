using Shouldly;
using JournalRecall.AI.Core;
using JournalRecall.AI.Tests.Fakes;

namespace JournalRecall.AI.Tests.Runtime;

public class RunnerResilienceTests
{
    private static AgentDefinition Def() =>
        Agent.Define("a").UsingModel(RunnerHarness.ModelName).WithMaxTurns(5).Build();

    [Fact]
    public async Task Transient_model_fault_is_retried_then_succeeds()
    {
        var client = new FakeChatClient()
            .Throws(new HttpRequestException("transient"))
            .RespondsWithText("recovered");
        var runner = RunnerHarness.Build(client, retryAnyFaultInstantly: true);

        var outcome = await runner.RunAsync(Def(), Conversation.FromUser("hi"), new RunContext());

        outcome.ShouldBeOfType<AgentOutcome.Completed>();
        client.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task Hard_model_fault_after_retries_fails_the_run()
    {
        var client = new FakeChatClient()
            .Throws(new HttpRequestException("1"))
            .Throws(new HttpRequestException("2"))
            .Throws(new HttpRequestException("3"))
            .Throws(new HttpRequestException("4"));
        var runner = RunnerHarness.Build(client, retryAnyFaultInstantly: true);

        var events = new List<AgentEvent>();
        await foreach (var e in runner.StreamAsync(Def(), Conversation.FromUser("hi"), new RunContext()))
            events.Add(e);

        events[^1].ShouldBeOfType<AgentEvent.Failed>();
    }
}
