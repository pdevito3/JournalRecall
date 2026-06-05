using Microsoft.Extensions.DependencyInjection;

namespace JournalRecall.AI.DependencyInjection;

/// <summary>
/// Fluent configuration surface returned by <c>AddJournalRecallAgents()</c>. Every knob is opt-in with a
/// sensible default (CONTEXT.md "Configuration"). Capabilities are layered on in later phases
/// (models, persistence, streaming, telemetry).
/// </summary>
public interface IJournalRecallAgentsBuilder
{
    /// <summary>The underlying service collection, for advanced wiring.</summary>
    IServiceCollection Services { get; }
}
