namespace JournalRecall.Api.Domain.Sessions.Dtos;

/// <summary>
/// A Session as the client sees it: the Raw draft plus the AI-derived Cleaned copy, Synopsis, the
/// effective <see cref="Sessions.CleanupStatus"/> (Stale derived), and the manual metadata (Topics,
/// People, Moods). <see cref="CleanedDraft"/>/<see cref="Synopsis"/> are empty until a Cleanup succeeds.
/// </summary>
public sealed record SessionDto(
    Guid Id,
    DateTimeOffset CreatedAt,
    string RawDraft,
    string CleanedDraft,
    string Synopsis,
    CleanupStatus CleanupStatus,
    bool CleanedHasHandEdits,
    IReadOnlyList<string> Topics,
    IReadOnlyList<string> People,
    IReadOnlyList<string> Moods,
    IReadOnlyList<SuggestionDto> Suggestions,
    LocationDto? Location)
{
    /// <summary>
    /// Projects a loaded Session, surfacing its effective status, metadata, and AI Suggestions. People are
    /// directory references; the caller resolves their display labels (<paramref name="peopleLabels"/>).
    /// </summary>
    public static SessionDto From(Session session, IReadOnlyList<string> peopleLabels) => new(
        session.Id, session.CreatedAt, session.RawDraft, session.CleanedDraft, session.Synopsis,
        session.EffectiveCleanupStatus, session.CleanedHasHandEdits,
        session.Topics.Select(t => t.Name).ToList(),
        peopleLabels,
        session.Moods.ToList(),
        session.Suggestions.Select(s => new SuggestionDto(s.Kind, s.Value)).ToList(),
        session.Location is { } location ? new LocationDto(location.Latitude, location.Longitude) : null);
}

/// <summary>A Session's captured geo-point (CONTEXT.md Location): coordinates only.</summary>
public sealed record LocationDto(double Latitude, double Longitude);
