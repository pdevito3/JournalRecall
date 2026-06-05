namespace JournalRecall.AI.Core;

/// <summary>
/// Fluent surface for authoring an <see cref="AgentDefinition"/>. The same contract backs inline
/// <c>Agent.Define(...)</c> and the <c>IAgent.Configure(IAgentBuilder)</c> discovery hook (ADR-0004).
/// <see cref="Build"/> is pure.
/// </summary>
public interface IAgentBuilder
{
    /// <summary>Selects the logical model name, resolved to a keyed <c>IChatClient</c> at run time.</summary>
    IAgentBuilder UsingModel(string logicalName);

    /// <summary>Sets the system prompt / instructions.</summary>
    IAgentBuilder WithInstructions(string instructions);

    /// <summary>Declares a tool the agent may invoke, with an optional authorization scope and error policy.</summary>
    IAgentBuilder WithTool(string name, string? scope = null, int maxRetries = 2, bool failClosed = false);

    /// <summary>Declares a DI-backed <see cref="ITool"/>, reading its name/description/scope from the type.</summary>
    IAgentBuilder WithTool<T>(int maxRetries = 2, bool failClosed = false) where T : ITool;

    /// <summary>Declares a read-only resource and how its content is surfaced (pinned vs discoverable).</summary>
    IAgentBuilder WithResource(string name, ResourceMode mode, string? scope = null);

    /// <summary>Declares a DI-backed <see cref="IResource"/> and how its content is surfaced.</summary>
    IAgentBuilder WithResource<T>(ResourceMode mode) where T : IResource;

    /// <summary>Declares a reusable templated prompt.</summary>
    IAgentBuilder WithPrompt(string name);

    /// <summary>Declares a DI-backed <see cref="IPrompt"/>, rendered into the system context.</summary>
    IAgentBuilder WithPrompt<T>() where T : IPrompt;

    /// <summary>Consumes tools/resources from a registered external MCP server (ADR-0003).</summary>
    IAgentBuilder WithMcpServer(string serverName, string? scope = null);

    /// <summary>Declares a sub-agent this agent may delegate to (surfaced as <c>agent.delegate</c>).</summary>
    IAgentBuilder CanCall(string agentName, string? scope = null);

    /// <summary>Declares a typed sub-agent to delegate to; its definition is built purely from <c>IAgent.Configure</c>.</summary>
    IAgentBuilder CanCall<T>(string? scope = null) where T : IAgent;

    /// <summary>Caps the number of model turns (ADR-0006).</summary>
    IAgentBuilder WithMaxTurns(int maxTurns);

    /// <summary>Caps the wall-clock run duration (ADR-0006).</summary>
    IAgentBuilder WithDeadline(TimeSpan maxDuration);

    /// <summary>Caps total token usage for the run (ADR-0006).</summary>
    IAgentBuilder WithTokenBudget(long tokens);

    /// <summary>Produces the immutable definition. Pure: performs no I/O.</summary>
    AgentDefinition Build();
}
