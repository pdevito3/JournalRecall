using JournalRecall.AI.Core;

namespace JournalRecall.AI.Runtime;

/// <summary>
/// The imperative shell that executes an agent. The library's primary surface — callable from
/// anywhere, not just HTTP (CONTEXT.md, ADR-0002). <see cref="StreamAsync"/> yields the rich event
/// stream; <see cref="RunAsync"/> drains it to the terminal <see cref="AgentOutcome"/>.
/// </summary>
public interface IAgentRunner
{
    /// <summary>Runs an agent to completion and returns its terminal outcome.</summary>
    Task<AgentOutcome> RunAsync(
        AgentDefinition definition,
        Conversation conversation,
        RunContext context,
        CancellationToken cancellationToken = default);

    /// <summary>Runs an agent, yielding lifecycle events as they occur. The terminal event carries the outcome.</summary>
    IAsyncEnumerable<AgentEvent> StreamAsync(
        AgentDefinition definition,
        Conversation conversation,
        RunContext context,
        CancellationToken cancellationToken = default);
}
