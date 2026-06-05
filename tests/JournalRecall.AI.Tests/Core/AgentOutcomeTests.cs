using Shouldly;
using Microsoft.Extensions.AI;
using JournalRecall.AI.Core;

namespace JournalRecall.AI.Tests.Core;

/// <summary>Phase 0 pure test: <see cref="AgentOutcome"/> is a value with structural equality.</summary>
public class AgentOutcomeTests
{
    [Fact]
    public void Stopped_outcomes_with_same_reason_are_value_equal()
    {
        var messages = new[] { new ChatMessage(ChatRole.Assistant, "hi") };

        var a = new AgentOutcome.Stopped(StopReason.MaxTurns, messages);
        var b = new AgentOutcome.Stopped(StopReason.MaxTurns, messages);

        a.ShouldBe(b);
    }

    [Fact]
    public void Outcome_variants_pattern_match_distinctly()
    {
        AgentOutcome completed = new AgentOutcome.Completed([]);
        AgentOutcome stopped = new AgentOutcome.Stopped(StopReason.Budget, []);
        AgentOutcome failed = new AgentOutcome.Failed("boom");

        Label(completed).ShouldBe("completed");
        Label(stopped).ShouldBe("stopped:Budget");
        Label(failed).ShouldBe("failed:boom");

        static string Label(AgentOutcome outcome) => outcome switch
        {
            AgentOutcome.Completed => "completed",
            AgentOutcome.Stopped s => $"stopped:{s.Reason}",
            AgentOutcome.Failed f => $"failed:{f.Reason}",
            _ => "?",
        };
    }
}
