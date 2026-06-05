using System.Text.Json.Serialization;

namespace JournalRecall.Api.Domain.Sessions;

/// <summary>
/// Where a Session stands with AI Cleanup (CONTEXT.md). Mostly derived: <see cref="Stale"/> is never
/// persisted — it is computed when the latest Raw Revision is newer than the last successful Cleanup
/// (see <see cref="Session.EffectiveCleanupStatus"/>). Serialized by name so the client reads stable
/// string statuses rather than ordinals.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CleanupStatus>))]
public enum CleanupStatus
{
    /// <summary>Cleanup has never run for this Session.</summary>
    NotRun,

    /// <summary>A Cleanup run is in progress.</summary>
    Running,

    /// <summary>The Cleaned copy is current with the Raw text.</summary>
    Clean,

    /// <summary>Raw has changed since the last successful Cleanup — a re-run is offered. Derived, not stored.</summary>
    Stale,

    /// <summary>The last Cleanup run failed; Raw and any prior Cleaned copy are untouched.</summary>
    Failed,
}
