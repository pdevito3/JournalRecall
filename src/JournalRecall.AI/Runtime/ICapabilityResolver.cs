using Microsoft.Extensions.AI;
using JournalRecall.AI.Core;

namespace JournalRecall.AI.Runtime;

/// <summary>
/// The live capabilities materialized for a single run: the tools the model can see (local, MCP,
/// synthetic resource tools, and delegation), plus any pinned resource/prompt content to inject as
/// context. Materialized per run from DI (ADR-0003).
/// </summary>
public sealed record MaterializedCapabilities
{
    public IReadOnlyList<AITool> Tools { get; init; } = [];
    public IReadOnlyList<ChatMessage> PinnedContext { get; init; } = [];

    /// <summary>
    /// Authorization descriptors for tools discovered at run time (e.g. MCP tools, whose names aren't
    /// known at <c>Build()</c>). The runner merges these into the definition so the pure
    /// <see cref="Core.AgentPolicy.Authorize"/> recognizes the calls.
    /// </summary>
    public IReadOnlyList<Core.ToolDescriptor> ExtraToolDescriptors { get; init; } = [];

    /// <summary>
    /// Progress events produced asynchronously during tool calls (e.g. MCP server progress
    /// notifications). The runner drains this after each tool dispatch and emits them on the stream.
    /// </summary>
    public System.Collections.Concurrent.ConcurrentQueue<Core.AgentEvent> ProgressEvents { get; init; } = new();

    public static readonly MaterializedCapabilities Empty = new();
}

/// <summary>
/// Materializes an agent definition's capability descriptors into live capabilities for a run, using
/// the run's DI scope so tool/resource instances can depend on scoped services.
/// </summary>
public interface ICapabilityResolver
{
    Task<MaterializedCapabilities> ResolveAsync(
        AgentDefinition definition,
        RunContext context,
        IServiceProvider scopedServices,
        CancellationToken cancellationToken);
}
