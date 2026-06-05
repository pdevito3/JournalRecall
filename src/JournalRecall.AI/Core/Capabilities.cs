namespace JournalRecall.AI.Core;

/// <summary>How a resource's content reaches the model (ADR-0003).</summary>
public enum ResourceMode
{
    /// <summary>Content injected as context on every model call.</summary>
    Pinned,

    /// <summary>Listed and fetched on demand via synthetic <c>list_resources</c>/<c>read_resource</c> tools.</summary>
    Discoverable,
}

/// <summary>
/// A pure, immutable description of a tool the agent may invoke. The live <c>ITool</c>/<c>AIFunction</c>
/// is materialized from DI per run (ADR-0003); the descriptor carries only what the pure core needs:
/// identity, authorization scope, and tool-error policy.
/// </summary>
public sealed record ToolDescriptor
{
    public required string Name { get; init; }

    /// <summary>Human/model-facing description surfaced in the tool's schema.</summary>
    public string? Description { get; init; }

    /// <summary>Authorization scope required to invoke this tool; null/empty means unrestricted.</summary>
    public string? Scope { get; init; }

    /// <summary>How many times a thrown tool error is fed back to the model before the run fails (ADR-0006).</summary>
    public int MaxRetries { get; init; } = 2;

    /// <summary>When true, any tool error fails the run immediately rather than feeding back for self-correction.</summary>
    public bool FailClosed { get; init; }

    /// <summary>Optional implementation type, used by the shell to resolve the live tool from DI (Phase 3).</summary>
    public Type? ImplementationType { get; init; }
}

/// <summary>A pure description of a read-only resource and how its content is surfaced.</summary>
public sealed record ResourceDescriptor
{
    public required string Name { get; init; }
    public required ResourceMode Mode { get; init; }

    /// <summary>Human/model-facing description, surfaced by <c>list_resources</c>.</summary>
    public string? Description { get; init; }

    /// <summary>Authorization scope required to read this resource; null/empty means unrestricted.</summary>
    public string? Scope { get; init; }

    public Type? ImplementationType { get; init; }
}

/// <summary>
/// A pure reference to an external MCP server whose tools/resources this agent consumes. The live
/// <c>McpClient</c> is a pooled DI singleton, materialized per run (ADR-0003). Tools discovered from
/// the server are authorized at <i>server</i> granularity via <see cref="Scope"/>.
/// </summary>
public sealed record McpServerRef
{
    /// <summary>Logical name of a registered MCP server (see <c>AddMcpServer</c>).</summary>
    public required string Name { get; init; }

    /// <summary>Scope required to invoke any tool from this server; null/empty means unrestricted.</summary>
    public string? Scope { get; init; }
}

/// <summary>A pure description of a reusable templated prompt/persona.</summary>
public sealed record PromptDescriptor
{
    public required string Name { get; init; }
    public Type? ImplementationType { get; init; }
}

/// <summary>
/// A pure description of a sub-agent this agent may delegate to via <c>.CanCall</c>. Compiled to a
/// tool internally and surfaced as <c>agent.delegate</c> (ADR-0003/0004), governed by the same scope
/// authorization as tools.
/// </summary>
public sealed record SubAgentDescriptor
{
    public required string Name { get; init; }

    /// <summary>Authorization scope required to delegate to this agent; null/empty means unrestricted.</summary>
    public string? Scope { get; init; }

    /// <summary>Optional agent type implementing the <c>IAgent</c> contract (resolved without a catalog, Phase 9).</summary>
    public Type? AgentType { get; init; }

    /// <summary>
    /// The sub-agent's definition, built purely at author time from <c>IAgent.Configure</c>. Lets the
    /// resolver compile delegation to a tool without reflection or a catalog (ADR-0004).
    /// </summary>
    public AgentDefinition? Definition { get; init; }
}
