using JournalRecall.AI.Core;

namespace JournalRecall.AI.Tests.Core;

/// <summary>Convenience constructors for building <see cref="AgentState"/> values in policy tests.</summary>
internal static class StateFactory
{
    public static readonly DateTimeOffset T0 = new(2026, 6, 2, 12, 0, 0, TimeSpan.Zero);

    public static AgentState State(
        AgentDefinition definition,
        RunContext? context = null,
        int turn = 0,
        long tokensUsed = 0,
        DateTimeOffset? now = null,
        bool cancelled = false,
        bool modelProducedFinalResponse = false,
        IReadOnlyDictionary<string, int>? toolRetries = null) => new()
    {
        Definition = definition,
        Context = context ?? new RunContext(),
        Turn = turn,
        TokensUsed = tokensUsed,
        StartedAt = T0,
        Now = now ?? T0,
        Cancelled = cancelled,
        ModelProducedFinalResponse = modelProducedFinalResponse,
        ToolRetries = toolRetries ?? new Dictionary<string, int>(StringComparer.Ordinal),
    };

    public static RunContext WithScopes(params string[] scopes) =>
        new() { Scopes = new HashSet<string>(scopes, StringComparer.Ordinal) };
}
