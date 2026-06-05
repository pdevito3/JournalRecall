using Microsoft.Extensions.AI;

namespace JournalRecall.AI.Core;

/// <summary>
/// A model-invocable action. Metadata is static (pure, AOT-friendly, readable by the builder without
/// instantiation); the live behavior is an M.E.AI <see cref="Delegate"/> that the resolver adapts to
/// an <see cref="AIFunction"/> carrying the tool's <see cref="Scope"/> (ADR-0003). Instances are
/// resolved from DI per run, so the handler may close over injected (scoped) services.
/// </summary>
public interface ITool
{
    static abstract string Name { get; }
    static abstract string? Description { get; }

    /// <summary>Authorization scope required to invoke; null/empty means unrestricted.</summary>
    static abstract string? Scope { get; }

    /// <summary>The handler delegate exposed to the model. Its parameters define the tool's JSON schema.</summary>
    Delegate Handler { get; }
}

/// <summary>Read-only context content fetched from an <see cref="IResource"/>.</summary>
public sealed record ResourceContent(string Text, string MimeType = "text/plain");

/// <summary>
/// Read-only context. Declared <c>Pinned</c> (content injected every call) or <c>Discoverable</c>
/// (fetched on demand via synthetic <c>list_resources</c>/<c>read_resource</c> tools) at the agent
/// definition (ADR-0003). The mode is the agent's choice, not the resource's.
/// </summary>
public interface IResource
{
    static abstract string Name { get; }
    static abstract string? Description { get; }
    static abstract string? Scope { get; }

    Task<ResourceContent> ReadAsync(RunContext context, CancellationToken cancellationToken);
}

/// <summary>A reusable templated persona/instruction, rendered into the agent's system context.</summary>
public interface IPrompt
{
    static abstract string Name { get; }

    string Render(RunContext context);
}

/// <summary>
/// Optional marker for a <i>discoverable</i> agent: a static name plus a pure <c>Configure</c> that
/// builds its definition. Backs by-type <c>AgentRef</c>, <c>.CanCall&lt;T&gt;()</c>, and (Phase 9)
/// the source-generated catalog. Inline <c>Agent.Define(...)</c> agents need not implement it
/// (ADR-0004).
/// </summary>
public interface IAgent
{
    static abstract string Name { get; }
    static abstract void Configure(IAgentBuilder builder);
}
