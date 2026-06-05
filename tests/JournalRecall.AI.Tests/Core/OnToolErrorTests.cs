using Shouldly;
using JournalRecall.AI.Core;
using static JournalRecall.AI.Tests.Core.StateFactory;

namespace JournalRecall.AI.Tests.Core;

public class OnToolErrorTests
{
    private static readonly Exception Boom = new InvalidOperationException("boom");

    [Fact]
    public void Feeds_error_back_within_retry_budget()
    {
        var def = Agent.Define("a").WithTool("t", maxRetries: 2).Build();
        var state = State(def, toolRetries: new Dictionary<string, int> { ["t"] = 0 });

        AgentPolicy.OnToolError(new ToolInvocation("t"), Boom, state)
            .ShouldBeOfType<ToolErrorDecision.FeedBack>();
    }

    [Fact]
    public void Fails_run_once_retries_exhausted()
    {
        var def = Agent.Define("a").WithTool("t", maxRetries: 2).Build();
        var state = State(def, toolRetries: new Dictionary<string, int> { ["t"] = 2 });

        AgentPolicy.OnToolError(new ToolInvocation("t"), Boom, state)
            .ShouldBeOfType<ToolErrorDecision.FailRun>();
    }

    [Fact]
    public void Fail_closed_tool_fails_immediately()
    {
        var def = Agent.Define("a").WithTool("t", maxRetries: 5, failClosed: true).Build();
        var state = State(def);

        AgentPolicy.OnToolError(new ToolInvocation("t"), Boom, state)
            .ShouldBeOfType<ToolErrorDecision.FailRun>();
    }

    [Fact]
    public void Unknown_tool_error_fails_run()
    {
        var def = Agent.Define("a").WithTool("known").Build();
        var state = State(def);

        AgentPolicy.OnToolError(new ToolInvocation("mystery"), Boom, state)
            .ShouldBeOfType<ToolErrorDecision.FailRun>();
    }

    [Fact]
    public void Zero_retry_tool_feeds_back_never()
    {
        var def = Agent.Define("a").WithTool("t", maxRetries: 0).Build();
        var state = State(def);

        AgentPolicy.OnToolError(new ToolInvocation("t"), Boom, state)
            .ShouldBeOfType<ToolErrorDecision.FailRun>();
    }
}
