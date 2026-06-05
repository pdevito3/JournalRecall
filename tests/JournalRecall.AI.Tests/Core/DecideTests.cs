using Shouldly;
using JournalRecall.AI.Core;
using static JournalRecall.AI.Tests.Core.StateFactory;

namespace JournalRecall.AI.Tests.Core;

public class DecideTests
{
    private static AgentDefinition Def(int? maxTurns = null, long? budget = null, TimeSpan? maxDuration = null) =>
        new() { Name = "a", MaxTurns = maxTurns, TokenBudget = budget, MaxDuration = maxDuration };

    [Fact]
    public void Continues_when_no_guardrail_tripped()
    {
        var state = State(Def(maxTurns: 5), turn: 2);
        AgentPolicy.Decide(state).ShouldBeOfType<Decision.Continue>();
    }

    [Fact]
    public void Completes_when_model_produced_final_response()
    {
        var state = State(Def(maxTurns: 5), modelProducedFinalResponse: true);
        AgentPolicy.Decide(state).ShouldBeOfType<Decision.Complete>();
    }

    [Fact]
    public void Stops_on_max_turns_reached()
    {
        var state = State(Def(maxTurns: 3), turn: 3);
        AgentPolicy.Decide(state).ShouldBe(new Decision.Stop(StopReason.MaxTurns));
    }

    [Fact]
    public void Stops_on_token_budget_reached()
    {
        var state = State(Def(budget: 1000), tokensUsed: 1000);
        AgentPolicy.Decide(state).ShouldBe(new Decision.Stop(StopReason.Budget));
    }

    [Fact]
    public void Stops_on_deadline_from_definition_max_duration()
    {
        var def = Def(maxDuration: TimeSpan.FromSeconds(10));
        var state = State(def, now: T0.AddSeconds(10));
        AgentPolicy.Decide(state).ShouldBe(new Decision.Stop(StopReason.Duration));
    }

    [Fact]
    public void Stops_on_deadline_from_run_context()
    {
        var ctx = new RunContext { Deadline = T0.AddSeconds(5) };
        var state = State(Def(), context: ctx, now: T0.AddSeconds(6));
        AgentPolicy.Decide(state).ShouldBe(new Decision.Stop(StopReason.Duration));
    }

    [Fact]
    public void Effective_deadline_is_the_earliest_of_context_and_definition()
    {
        var ctx = new RunContext { Deadline = T0.AddSeconds(20) };
        var def = Def(maxDuration: TimeSpan.FromSeconds(5));
        var state = State(def, context: ctx, now: T0.AddSeconds(6));
        AgentPolicy.Decide(state).ShouldBe(new Decision.Stop(StopReason.Duration));
    }

    [Fact]
    public void Cancellation_takes_precedence_over_everything()
    {
        var state = State(Def(maxTurns: 1), turn: 5, cancelled: true, modelProducedFinalResponse: true);
        AgentPolicy.Decide(state).ShouldBe(new Decision.Stop(StopReason.Cancelled));
    }

    [Fact]
    public void Completion_wins_over_guardrails()
    {
        // At max turns AND over budget, but the model gave a final answer: that answer wins.
        var def = Def(maxTurns: 3, budget: 100);
        var state = State(def, turn: 3, tokensUsed: 500, modelProducedFinalResponse: true);
        AgentPolicy.Decide(state).ShouldBeOfType<Decision.Complete>();
    }

    [Fact]
    public void Unbounded_definition_always_continues()
    {
        var state = State(Def(), turn: 1000, tokensUsed: long.MaxValue);
        AgentPolicy.Decide(state).ShouldBeOfType<Decision.Continue>();
    }
}
