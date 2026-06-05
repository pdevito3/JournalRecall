using Shouldly;
using JournalRecall.AI.Core;
using JournalRecall.AI.Tests.Fakes;

namespace JournalRecall.AI.Tests.Runtime;

public class DelegationTests
{
    [Fact]
    public void CanCall_of_T_builds_the_sub_agent_definition_purely()
    {
        var def = Agent.Define("planner").UsingModel(RunnerHarness.ModelName)
            .CanCall<EchoAgent>(scope: "delegate:echo").Build();

        var sub = def.SubAgents.ShouldHaveSingleItem();
        sub.Name.ShouldBe("echoer");
        sub.Scope.ShouldBe("delegate:echo");
        sub.AgentType.ShouldBe(typeof(EchoAgent));
        sub.Definition!.Instructions.ShouldBe("Echo the input.");
    }

    [Fact]
    public async Task Delegation_runs_the_sub_agent_and_returns_its_answer()
    {
        // Shared fake model: parent turn 1 delegates, sub-agent answers, parent turn 2 finalizes.
        var client = new FakeChatClient()
            .RequestsTool("echoer", new Dictionary<string, object?> { ["input"] = "say hi" })
            .RespondsWithText("sub said hi")          // the sub-agent's only turn
            .RespondsWithText("delegation complete");  // parent's final turn
        var runner = RunnerHarness.Build(client);

        var def = Agent.Define("planner").UsingModel(RunnerHarness.ModelName)
            .CanCall<EchoAgent>().WithMaxTurns(5).Build();

        var events = new List<AgentEvent>();
        await foreach (var e in runner.StreamAsync(def, Conversation.FromUser("delegate please"), new RunContext()))
            events.Add(e);

        events.OfType<AgentEvent.AgentDelegated>().ShouldContain(d => d.AgentName == "echoer");
        events[^1].ShouldBeOfType<AgentEvent.Completed>();
        client.CallCount.ShouldBe(3);
    }

    [Fact]
    public async Task Delegation_is_denied_without_required_scope()
    {
        var client = new FakeChatClient()
            .RequestsTool("echoer", new Dictionary<string, object?> { ["input"] = "x" })
            .RespondsWithText("ok");
        var runner = RunnerHarness.Build(client);

        var def = Agent.Define("planner").UsingModel(RunnerHarness.ModelName)
            .CanCall<EchoAgent>(scope: "delegate:echo").WithMaxTurns(5).Build();

        var events = new List<AgentEvent>();
        await foreach (var e in runner.StreamAsync(def, Conversation.FromUser("go"), new RunContext()))
            events.Add(e);

        events.OfType<AgentEvent.ToolFailed>().ShouldContain(f => f.ToolName == "echoer");
        events.OfType<AgentEvent.AgentDelegated>().ShouldBeEmpty();
    }
}
