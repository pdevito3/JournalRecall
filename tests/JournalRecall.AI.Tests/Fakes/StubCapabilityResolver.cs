using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using JournalRecall.AI.Core;
using JournalRecall.AI.Runtime;

namespace JournalRecall.AI.Tests.Fakes;

/// <summary>Returns a fixed set of tools/pinned context (and optional progress queue) for runner tests.</summary>
internal sealed class StubCapabilityResolver(
    IReadOnlyList<AITool>? tools = null,
    IReadOnlyList<ChatMessage>? pinned = null,
    ConcurrentQueue<AgentEvent>? progress = null) : ICapabilityResolver
{
    public Task<MaterializedCapabilities> ResolveAsync(
        AgentDefinition definition,
        RunContext context,
        IServiceProvider scopedServices,
        CancellationToken cancellationToken) =>
        Task.FromResult(new MaterializedCapabilities
        {
            Tools = tools ?? [],
            PinnedContext = pinned ?? [],
            ProgressEvents = progress ?? new ConcurrentQueue<AgentEvent>(),
        });
}
