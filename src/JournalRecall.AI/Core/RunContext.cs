namespace JournalRecall.AI.Core;

/// <summary>
/// Per-run value built by the consumer at the edge (claims → scopes/tenant, correlation id,
/// deadline). The library imposes no auth scheme and has no HTTP coupling (ADR-0002).
/// </summary>
public sealed record RunContext
{
    /// <summary>The authenticated caller's subject/identifier, if any.</summary>
    public string? Subject { get; init; }

    /// <summary>Authorization scopes granted to the caller. Tool/resource/delegation scopes are checked against this set.</summary>
    public IReadOnlySet<string> Scopes { get; init; } = EmptySet;

    /// <summary>Tenant the run executes under, for multi-tenant consumers.</summary>
    public string? Tenant { get; init; }

    /// <summary>Correlation id threaded through events and telemetry.</summary>
    public string CorrelationId { get; init; } = Guid.CreateVersion7().ToString("n");

    /// <summary>BCP-47 locale hint, if the consumer supplies one.</summary>
    public string? Locale { get; init; }

    /// <summary>Absolute wall-clock deadline for the whole run. Combined with the definition's max duration.</summary>
    public DateTimeOffset? Deadline { get; init; }

    private static readonly IReadOnlySet<string> EmptySet =
        new HashSet<string>(0, StringComparer.Ordinal);

    /// <summary>True when <paramref name="scope"/> is null/empty (no scope required) or held by the caller.</summary>
    public bool HasScope(string? scope) =>
        string.IsNullOrEmpty(scope) || Scopes.Contains(scope);
}
