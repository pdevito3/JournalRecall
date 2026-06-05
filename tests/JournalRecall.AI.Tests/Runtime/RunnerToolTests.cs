using Shouldly;
using JournalRecall.AI.Core;
using JournalRecall.AI.Tests.Fakes;

namespace JournalRecall.AI.Tests.Runtime;

public class RunnerToolTests
{
    [Fact]
    public async Task Dispatches_tool_then_completes()
    {
        var client = new FakeChatClient()
            .RequestsTool("echo", ToolFakes.TextArg("hi"))
            .RespondsWithText("the tool said hi");
        var runner = RunnerHarness.Build(client, new StubCapabilityResolver([ToolFakes.Echo()]));

        var def = Agent.Define("a").UsingModel(RunnerHarness.ModelName).WithTool("echo").WithMaxTurns(5).Build();

        var events = new List<AgentEvent>();
        await foreach (var e in runner.StreamAsync(def, Conversation.FromUser("go"), new RunContext()))
            events.Add(e);

        events.OfType<AgentEvent.ToolInvoking>().ShouldContain(i => i.ToolName == "echo");
        events.OfType<AgentEvent.ToolSucceeded>().ShouldContain(s => s.ToolName == "echo");
        events[^1].ShouldBeOfType<AgentEvent.Completed>();
        client.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task Unauthorized_tool_is_denied_and_fed_back_for_self_correction()
    {
        var client = new FakeChatClient()
            .RequestsTool("write", ToolFakes.TextArg("x"))
            .RespondsWithText("ok, I won't");
        var runner = RunnerHarness.Build(client, new StubCapabilityResolver([
            Microsoft.Extensions.AI.AIFunctionFactory.Create((string text) => text, "write")]));

        // Caller lacks 'recipes:write'.
        var def = Agent.Define("a").UsingModel(RunnerHarness.ModelName)
            .WithTool("write", scope: "recipes:write").WithMaxTurns(5).Build();

        var events = new List<AgentEvent>();
        await foreach (var e in runner.StreamAsync(def, Conversation.FromUser("write please"), new RunContext()))
            events.Add(e);

        events.OfType<AgentEvent.ToolFailed>()
            .ShouldContain(f => f.ToolName == "write" && f.Error.Contains("scope"));
        events.OfType<AgentEvent.ToolInvoking>().ShouldBeEmpty();
        events[^1].ShouldBeOfType<AgentEvent.Completed>();
        client.CallCount.ShouldBe(2); // denial fed back; model self-corrected on turn 2
    }

    [Fact]
    public async Task Authorized_tool_runs_when_caller_holds_scope()
    {
        var client = new FakeChatClient()
            .RequestsTool("write", ToolFakes.TextArg("x"))
            .RespondsWithText("done");
        var runner = RunnerHarness.Build(client, new StubCapabilityResolver([
            Microsoft.Extensions.AI.AIFunctionFactory.Create((string text) => text, "write")]));

        var def = Agent.Define("a").UsingModel(RunnerHarness.ModelName)
            .WithTool("write", scope: "recipes:write").WithMaxTurns(5).Build();
        var ctx = new RunContext { Scopes = new HashSet<string> { "recipes:write" } };

        var events = new List<AgentEvent>();
        await foreach (var e in runner.StreamAsync(def, Conversation.FromUser("write"), ctx))
            events.Add(e);

        events.OfType<AgentEvent.ToolSucceeded>().ShouldContain(s => s.ToolName == "write");
    }

    [Fact]
    public async Task Tool_error_is_fed_back_within_retry_budget()
    {
        var client = new FakeChatClient()
            .RequestsTool("boom")
            .RespondsWithText("recovered");
        var runner = RunnerHarness.Build(client, new StubCapabilityResolver([ToolFakes.AlwaysThrows("boom")]));

        var def = Agent.Define("a").UsingModel(RunnerHarness.ModelName)
            .WithTool("boom", maxRetries: 2).WithMaxTurns(5).Build();

        var outcome = await runner.RunAsync(def, Conversation.FromUser("go"), new RunContext());

        outcome.ShouldBeOfType<AgentOutcome.Completed>();
        client.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task Fail_closed_tool_error_fails_the_run()
    {
        var client = new FakeChatClient().RequestsTool("boom");
        var runner = RunnerHarness.Build(client, new StubCapabilityResolver([ToolFakes.AlwaysThrows("boom")]));

        var def = Agent.Define("a").UsingModel(RunnerHarness.ModelName)
            .WithTool("boom", failClosed: true).WithMaxTurns(5).Build();

        var events = new List<AgentEvent>();
        await foreach (var e in runner.StreamAsync(def, Conversation.FromUser("go"), new RunContext()))
            events.Add(e);

        events.OfType<AgentEvent.ToolFailed>().ShouldNotBeEmpty();
        events[^1].ShouldBeOfType<AgentEvent.Failed>();
    }
}
