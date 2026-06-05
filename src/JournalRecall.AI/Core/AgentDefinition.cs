namespace JournalRecall.AI.Core;

/// <summary>
/// The immutable, cheap-to-build value the fluent builder produces: model selection, instructions,
/// capability descriptors, and per-run policies. Constructing it does <b>no I/O</b> (ADR-0001).
/// </summary>
public sealed record AgentDefinition
{
    /// <summary>Stable agent name (used for delegation, discovery, and telemetry).</summary>
    public required string Name { get; init; }

    /// <summary>Logical model name resolved to a keyed <c>IChatClient</c> at run time (ADR-0002).</summary>
    public string? ModelName { get; init; }

    /// <summary>System prompt / instructions for the agent.</summary>
    public string? Instructions { get; init; }

    public IReadOnlyList<ToolDescriptor> Tools { get; init; } = [];
    public IReadOnlyList<ResourceDescriptor> Resources { get; init; } = [];
    public IReadOnlyList<PromptDescriptor> Prompts { get; init; } = [];
    public IReadOnlyList<SubAgentDescriptor> SubAgents { get; init; } = [];
    public IReadOnlyList<McpServerRef> McpServers { get; init; } = [];

    /// <summary>Maximum model turns before the run is stopped (ADR-0006). Null = unbounded.</summary>
    public int? MaxTurns { get; init; }

    /// <summary>Maximum wall-clock duration for the run. Combined with <see cref="RunContext.Deadline"/>. Null = unbounded.</summary>
    public TimeSpan? MaxDuration { get; init; }

    /// <summary>Token budget for the run (ADR-0006). Null = unbounded.</summary>
    public long? TokenBudget { get; init; }

    /// <summary>Looks up a tool descriptor by name (used by authorization and tool-error policy).</summary>
    public ToolDescriptor? FindTool(string name) =>
        Tools.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.Ordinal));

    /// <summary>Looks up a sub-agent descriptor by its delegation tool/agent name.</summary>
    public SubAgentDescriptor? FindSubAgent(string name) =>
        SubAgents.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.Ordinal));
}
