namespace JournalRecall.Api.Domain.Sessions.Dtos;

/// <summary>
/// A Session as the client sees it: the Raw draft plus the AI-derived Cleaned copy, Synopsis, the
/// effective <see cref="Sessions.CleanupStatus"/> (Stale derived), and the manual metadata (Topics,
/// People, Mood). <see cref="CleanedDraft"/>/<see cref="Synopsis"/> are empty until a Cleanup succeeds.
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
    MoodDto? Mood,
    IReadOnlyList<SuggestionDto> Suggestions,
    LocationDto? Location)
{
    /// <summary>Projects a loaded Session, surfacing its effective status, metadata, and AI Suggestions.</summary>
    public static SessionDto From(Session session) => new(
        session.Id, session.CreatedAt, session.RawDraft, session.CleanedDraft, session.Synopsis,
        session.EffectiveCleanupStatus, session.CleanedHasHandEdits,
        session.Topics.Select(t => t.Name).ToList(),
        session.People.Select(p => p.Name).ToList(),
        session.Mood is { } mood ? new MoodDto(mood.Key, mood.CustomValue) : null,
        session.Suggestions.Select(s => new SuggestionDto(s.Kind, s.Value, s.MoodCustomValue)).ToList(),
        session.Location is { } location ? new LocationDto(location.Latitude, location.Longitude) : null);
}

/// <summary>A Session's captured geo-point (CONTEXT.md Location): coordinates only.</summary>
public sealed record LocationDto(double Latitude, double Longitude);
