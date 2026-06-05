using Shouldly;
using FsCheck;
using FsCheck.Xunit;
using JournalRecall.AI.Core;
using static JournalRecall.AI.Tests.Core.StateFactory;

namespace JournalRecall.AI.Tests.Core;

public class PolicyPropertyTests
{
    [Property]
    public Property Authorize_allows_scoped_tool_iff_caller_holds_the_scope(NonEmptyString scope, bool grant)
    {
        var s = scope.Get;
        var def = Agent.Define("a").WithTool("t", scope: s).Build();
        var ctx = grant ? WithScopes(s) : new RunContext();

        var allowed = AgentPolicy.Authorize(new ToolInvocation("t"), State(def, context: ctx)).IsAllowed;

        return (allowed == grant).ToProperty();
    }

    [Property]
    public Property Cancellation_always_stops_regardless_of_other_state(
        int turn, long tokens, bool finalResponse)
    {
        var def = new AgentDefinition { Name = "a", MaxTurns = 10, TokenBudget = 100 };
        var state = State(def, turn: turn, tokensUsed: tokens,
            cancelled: true, modelProducedFinalResponse: finalResponse);

        return (AgentPolicy.Decide(state) is Decision.Stop { Reason: StopReason.Cancelled }).ToProperty();
    }

    [Property]
    public Property Budget_stop_triggers_exactly_when_used_meets_budget(
        PositiveInt budget, NonNegativeInt used)
    {
        var def = new AgentDefinition { Name = "a", TokenBudget = budget.Get };
        var state = State(def, tokensUsed: used.Get);

        var stoppedForBudget = AgentPolicy.Decide(state) is Decision.Stop { Reason: StopReason.Budget };
        return (stoppedForBudget == (used.Get >= budget.Get)).ToProperty();
    }

    [Property]
    public Property OnToolError_feeds_back_iff_retries_remain_and_not_fail_closed(
        NonNegativeInt maxRetries, NonNegativeInt soFar, bool failClosed)
    {
        var def = Agent.Define("a").WithTool("t", maxRetries: maxRetries.Get, failClosed: failClosed).Build();
        var state = State(def, toolRetries: new Dictionary<string, int> { ["t"] = soFar.Get });

        var decision = AgentPolicy.OnToolError(new ToolInvocation("t"), new Exception("x"), state);
        var feedsBack = decision is ToolErrorDecision.FeedBack;
        var expected = !failClosed && soFar.Get < maxRetries.Get;

        return (feedsBack == expected).ToProperty();
    }

    [Property]
    public Property Decide_is_deterministic(int turn, long tokens, bool cancelled, bool final)
    {
        var def = new AgentDefinition { Name = "a", MaxTurns = 5, TokenBudget = 1000 };
        var state = State(def, turn: turn, tokensUsed: tokens,
            cancelled: cancelled, modelProducedFinalResponse: final);

        return (AgentPolicy.Decide(state) == AgentPolicy.Decide(state)).ToProperty();
    }
}
