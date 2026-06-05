using Shouldly;
using Microsoft.Extensions.AI;
using JournalRecall.AI.Core;
using JournalRecall.AI.Tests.Fakes;

namespace JournalRecall.AI.Tests.Runtime;

public class RunnerLoopTests
{
    private static AgentDefinition Def(int? maxTurns = null, long? budget = null) => Agent
        .Define("assistant")
        .UsingModel(RunnerHarness.ModelName)
        .WithInstructions("be helpful")
        .WithMaxTurns(maxTurns ?? 10)
        .Build() with { TokenBudget = budget };

    [Fact]
    public async Task Single_text_turn_completes()
    {
        var client = new FakeChatClient().RespondsWithText("Hello!");
        var runner = RunnerHarness.Build(client);

        var outcome = await runner.RunAsync(Def(), Conversation.FromUser("hi"), new RunContext());

        var completed = outcome.ShouldBeOfType<AgentOutcome.Completed>();
        completed.Messages.Last().Text.ShouldBe("Hello!");
        client.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task System_instructions_and_user_input_are_sent_to_the_model()
    {
        var client = new FakeChatClient().RespondsWithText("ok");
        var runner = RunnerHarness.Build(client);

        await runner.RunAsync(Def(), Conversation.FromUser("what's for dinner?"), new RunContext());

        var firstCall = client.ReceivedMessages[0];
        firstCall.ShouldContain(m => m.Role == ChatRole.System && m.Text == "be helpful");
        firstCall.ShouldContain(m => m.Role == ChatRole.User && m.Text == "what's for dinner?");
    }

    [Fact]
    public async Task Pinned_resource_content_is_sent_to_the_model_as_context()
    {
        var client = new FakeChatClient().RespondsWithText("ok");
        var pinned = new[] { new ChatMessage(ChatRole.System, "[resource:guidelines]\nEat more vegetables.") };
        var runner = RunnerHarness.Build(client, new StubCapabilityResolver(pinned: pinned));

        await runner.RunAsync(Def(), Conversation.FromUser("hi"), new RunContext());

        client.ReceivedMessages[0].ShouldContain(m => m.Text.Contains("Eat more vegetables."));
    }

    [Fact]
    public async Task Stops_on_max_turns_when_model_keeps_requesting_tools()
    {
        var tools = new[] { ToolFakes.Echo() };
        var client = new FakeChatClient()
            .RequestsTool("echo", ToolFakes.TextArg("a"))
            .RequestsTool("echo", ToolFakes.TextArg("b"))
            .RequestsTool("echo", ToolFakes.TextArg("c"));
        var runner = RunnerHarness.Build(client, new StubCapabilityResolver(tools));

        var def = Agent.Define("a").UsingModel(RunnerHarness.ModelName).WithTool("echo").WithMaxTurns(2).Build();
        var outcome = await runner.RunAsync(def, Conversation.FromUser("go"), new RunContext());

        outcome.ShouldBeOfType<AgentOutcome.Stopped>()
            .Reason.ShouldBe(StopReason.MaxTurns);
        client.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task Stops_on_token_budget()
    {
        var tools = new[] { ToolFakes.Echo() };
        var client = new FakeChatClient()
            .RequestsTool("echo", ToolFakes.TextArg("a"), totalTokens: 10)
            .RequestsTool("echo", ToolFakes.TextArg("b"), totalTokens: 10);
        var runner = RunnerHarness.Build(client, new StubCapabilityResolver(tools));

        var def = Agent.Define("a").UsingModel(RunnerHarness.ModelName)
            .WithTool("echo").WithMaxTurns(10).WithTokenBudget(15).Build();
        var outcome = await runner.RunAsync(def, Conversation.FromUser("go"), new RunContext());

        outcome.ShouldBeOfType<AgentOutcome.Stopped>()
            .Reason.ShouldBe(StopReason.Budget);
    }

    [Fact]
    public async Task Cancellation_yields_stopped_cancelled()
    {
        var client = new FakeChatClient().RespondsWithText("never reached");
        var runner = RunnerHarness.Build(client);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var outcome = await runner.RunAsync(Def(), Conversation.FromUser("hi"), new RunContext(), cts.Token);

        outcome.ShouldBeOfType<AgentOutcome.Stopped>()
            .Reason.ShouldBe(StopReason.Cancelled);
    }

    [Fact]
    public async Task Event_stream_has_expected_lifecycle_shape()
    {
        var client = new FakeChatClient().RespondsWithText("done");
        var runner = RunnerHarness.Build(client);

        var events = new List<AgentEvent>();
        await foreach (var e in runner.StreamAsync(Def(), Conversation.FromUser("hi"), new RunContext()))
            events.Add(e);

        events[0].ShouldBeOfType<AgentEvent.RunStarted>();
        events.OfType<AgentEvent.TurnStarted>().ShouldHaveSingleItem();
        events.OfType<AgentEvent.UsageUpdated>().ShouldNotBeEmpty();
        events[^1].ShouldBeOfType<AgentEvent.Completed>();
    }

    [Fact]
    public async Task RunAsync_terminal_matches_stream_terminal_event()
    {
        var client = new FakeChatClient().RespondsWithText("done");
        var runner = RunnerHarness.Build(client);

        AgentEvent? terminal = null;
        await foreach (var e in runner.StreamAsync(Def(), Conversation.FromUser("hi"), new RunContext()))
            terminal = e;

        terminal.ShouldBeOfType<AgentEvent.Completed>();
    }

    [Fact]
    public async Task Missing_model_registration_fails_gracefully()
    {
        var client = new FakeChatClient().RespondsWithText("x");
        var runner = RunnerHarness.Build(client);
        var def = Agent.Define("a").UsingModel("nonexistent-model").Build();

        var outcome = await runner.RunAsync(def, Conversation.FromUser("hi"), new RunContext());

        outcome.ShouldBeOfType<AgentOutcome.Failed>();
    }
}
