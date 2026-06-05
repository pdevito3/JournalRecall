namespace JournalRecall.AI.Core;

/// <summary>
/// Opt-in, source-generated registry of discoverable (<see cref="IAgent"/>) agents, enabling by-name
/// resolution and discovery (ADR-0004). Populated by the Phase 9 generator; absent for inline-only
/// consumers (by-name resolution then throws a clear error). Strictly additive.
/// </summary>
public interface IAgentCatalog
{
    /// <summary>Names of all cataloged agents.</summary>
    IReadOnlyCollection<string> Names { get; }

    /// <summary>Resolves a cataloged agent definition by name, or null if not found.</summary>
    AgentDefinition? Find(string name);
}
