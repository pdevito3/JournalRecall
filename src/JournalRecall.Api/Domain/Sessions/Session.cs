using JournalRecall.Api.Domain.Sessions.DomainEvents;
using JournalRecall.Api.Domain.Sessions.Metadata;

namespace JournalRecall.Api.Domain.Sessions;

/// <summary>
/// The core aggregate (CONTEXT.md): one act of journaling started by a User on a given day. Owns the
/// Raw text the user wrote. There is no InProgress/Completed lifecycle — a Session simply exists.
///
/// Phase 2 (this slice) carries only the live, autosaved <see cref="RawDraft"/>; the append-only Raw
/// Revision stream minted at save points lands in issue 0005. Raw is human-owned and stored exactly as
/// typed — never mutated by the server.
/// </summary>
public sealed class Session : BaseEntity
{
    private readonly List<RawRevision> _rawRevisions = [];
    private readonly List<CleanedRevision> _cleanedRevisions = [];
    private readonly List<SessionTopic> _topics = [];
    private readonly List<SessionPerson> _people = [];
    private readonly List<MetadataSuggestion> _suggestions = [];

    public Guid UserId { get; private set; }
    public string RawDraft { get; private set; } = string.Empty;

    /// <summary>The optional captured latitude/longitude (CONTEXT.md Location); null unless geo opt-in stamped one.</summary>
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }

    /// <summary>The captured geo-point as a value object, or null when none was stored.</summary>
    public Location? Location => Sessions.Location.TryCreate(Latitude, Longitude, out var location) ? location : null;

    /// <summary>The AI-derived, polished copy produced by the latest Cleanup (CONTEXT.md). Empty until a run succeeds.</summary>
    public string CleanedDraft { get; private set; } = string.Empty;

    /// <summary>The short AI recap of this Session, written by Cleanup (CONTEXT.md). Empty until a run succeeds.</summary>
    public string Synopsis { get; private set; } = string.Empty;

    /// <summary>
    /// The stored coarse Cleanup status — never <see cref="Sessions.CleanupStatus.Stale"/>, which is
    /// derived (see <see cref="EffectiveCleanupStatus"/>).
    /// </summary>
    public CleanupStatus CleanupStatus { get; private set; } = CleanupStatus.NotRun;

    /// <summary>The Raw Revision number that the last successful Cleanup read. 0 when never cleaned.</summary>
    public int LastCleanedRawRevisionNumber { get; private set; }

    /// <summary>
    /// True when the current Cleaned copy carries user hand-edits made since the last AI run — the cue
    /// to warn before a re-run overwrites them (CONTEXT.md, ADR-0003). Cleared when Cleanup regenerates.
    /// </summary>
    public bool CleanedHasHandEdits { get; private set; }

    /// <summary>The Raw Revision number captured when the in-flight Cleanup began. Not persisted.</summary>
    private int _cleaningFromRawRevisionNumber;

    /// <summary>The append-only Raw Revision stream, oldest first (ADR-0003).</summary>
    public IReadOnlyList<RawRevision> RawRevisions => _rawRevisions;

    /// <summary>The append-only Cleaned Revision stream, oldest first (ADR-0003).</summary>
    public IReadOnlyList<CleanedRevision> CleanedRevisions => _cleanedRevisions;

    /// <summary>The Topic tags on this Session (user-set and any accepted AI Suggestions).</summary>
    public IReadOnlyList<SessionTopic> Topics => _topics;

    /// <summary>The People referenced on this Session (user-set and any accepted AI Suggestions).</summary>
    public IReadOnlyList<SessionPerson> People => _people;

    /// <summary>AI-proposed metadata awaiting accept/reject (CONTEXT.md). Distinct from accepted metadata.</summary>
    public IReadOnlyList<MetadataSuggestion> Suggestions => _suggestions;

    /// <summary>The mood key (a <see cref="Metadata.MoodType"/> name, known or Custom); null when no mood is set.</summary>
    public string? MoodKey { get; private set; }

    /// <summary>The free-text value for a Custom mood; null otherwise.</summary>
    public string? MoodCustomValue { get; private set; }

    /// <summary>The mood as a value object, or null when unset.</summary>
    public Mood? Mood => MoodKey is null ? null : Sessions.Metadata.Mood.Of(MoodKey, MoodCustomValue);

    /// <summary>The latest Raw Revision number (== the count, since numbers are sequential). 0 when empty.</summary>
    public int LatestRawRevisionNumber => _rawRevisions.Count;

    /// <summary>
    /// The Cleanup status as the user sees it: the stored status, except a <see cref="CleanupStatus.Clean"/>
    /// Session whose Raw has advanced past the last cleaned Revision reads as <see cref="CleanupStatus.Stale"/>
    /// (CONTEXT.md — "Stale means the latest Raw Revision is newer than the last successful Cleanup").
    /// </summary>
    public CleanupStatus EffectiveCleanupStatus =>
        CleanupStatus == CleanupStatus.Clean && LatestRawRevisionNumber > LastCleanedRawRevisionNumber
            ? CleanupStatus.Stale
            : CleanupStatus;

    private Session() { } // EF

    public static Session Create(Guid userId, Location? location = null)
    {
        var session = new Session
        {
            UserId = userId,
            Latitude = location?.Latitude,
            Longitude = location?.Longitude,
        };
        session.QueueDomainEvent(new SessionCreated(session.Id));
        return session;
    }

    /// <summary>
    /// Save point for Raw: the live Draft mutates to the user's text (verbatim), and when the content
    /// actually changed a new immutable Revision is appended. A debounced/explicit save that doesn't
    /// change the text mints nothing, so rapid keystrokes never each create a Revision.
    /// </summary>
    public void SaveDraft(string rawText)
    {
        rawText ??= string.Empty;
        var changed = !string.Equals(rawText, LatestRawContent, StringComparison.Ordinal);

        RawDraft = rawText;
        if (changed)
            _rawRevisions.Add(new RawRevision(_rawRevisions.Count + 1, rawText));
    }

    private string LatestRawContent => _rawRevisions.Count == 0 ? string.Empty : _rawRevisions[^1].Content;

    /// <summary>
    /// Marks the Session as having a Cleanup in flight and snapshots the Raw Revision the run will read,
    /// so the resulting Cleaned copy is pinned to that exact Raw version (later Raw edits flip it Stale).
    /// </summary>
    public void BeginCleanup()
    {
        _cleaningFromRawRevisionNumber = LatestRawRevisionNumber;
        CleanupStatus = CleanupStatus.Running;
    }

    /// <summary>
    /// A successful Cleanup: appends a Cleaned Revision, sets the current Cleaned copy + Synopsis, and
    /// marks the Session <see cref="CleanupStatus.Clean"/>. Raw is never touched (CONTEXT.md, ADR-0003).
    /// </summary>
    public void CompleteCleanup(string cleanedText, string synopsis)
    {
        cleanedText ??= string.Empty;
        CleanedDraft = cleanedText;
        Synopsis = synopsis ?? string.Empty;
        _cleanedRevisions.Add(new CleanedRevision(_cleanedRevisions.Count + 1, cleanedText));
        LastCleanedRawRevisionNumber = _cleaningFromRawRevisionNumber;
        CleanupStatus = CleanupStatus.Clean;
        // A fresh AI copy supersedes any prior hand-edits — the prior Revision stays in history.
        CleanedHasHandEdits = false;
    }

    /// <summary>A failed Cleanup: records the failure without corrupting Raw or any prior Cleaned copy.</summary>
    public void FailCleanup() => CleanupStatus = CleanupStatus.Failed;

    /// <summary>
    /// A user hand-edit of the Cleaned copy: appends a Cleaned Revision (when the text actually changed)
    /// and flags the copy as hand-edited so a later re-run warns first. Raw is never touched. Returns
    /// whether a new Revision was appended.
    /// </summary>
    public bool EditCleaned(string cleanedText)
    {
        cleanedText ??= string.Empty;
        if (string.Equals(cleanedText, CleanedDraft, StringComparison.Ordinal))
            return false;

        CleanedDraft = cleanedText;
        _cleanedRevisions.Add(new CleanedRevision(_cleanedRevisions.Count + 1, cleanedText));
        CleanedHasHandEdits = true;
        return true;
    }

    /// <summary>
    /// Replaces the user's Topic tags (provenance <see cref="MetadataProvenance.UserSet"/>), leaving any
    /// AI-provenance tags intact. Names are trimmed and de-duplicated case-insensitively.
    /// </summary>
    public void SetUserTopics(IEnumerable<string> names)
    {
        _topics.RemoveAll(t => t.Provenance == MetadataProvenance.UserSet);
        foreach (var name in Normalize(names))
            if (!_topics.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                _topics.Add(new SessionTopic(name, MetadataProvenance.UserSet));
    }

    /// <summary>Replaces the user's People tags (provenance <see cref="MetadataProvenance.UserSet"/>), leaving AI ones intact.</summary>
    public void SetUserPeople(IEnumerable<string> names)
    {
        _people.RemoveAll(p => p.Provenance == MetadataProvenance.UserSet);
        foreach (var name in Normalize(names))
            if (!_people.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                _people.Add(new SessionPerson(name, MetadataProvenance.UserSet));
    }

    /// <summary>Sets or clears the Session's mood.</summary>
    public void SetMood(Mood? mood)
    {
        MoodKey = mood?.Key;
        MoodCustomValue = mood?.CustomValue;
    }

    private static IEnumerable<string> Normalize(IEnumerable<string> names) => (names ?? [])
        .Select(n => n?.Trim() ?? string.Empty)
        .Where(n => n.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Replaces the pending AI Suggestions with a fresh set from a Cleanup run. Suggestions that would
    /// duplicate metadata the Session already carries are dropped (AI never duplicates or overwrites
    /// existing metadata, CONTEXT.md): an existing Topic/Person name, or any mood already set.
    /// </summary>
    public void ReplaceAiSuggestions(
        IEnumerable<string> topics, IEnumerable<string> people, Mood? mood)
    {
        _suggestions.Clear();

        foreach (var name in Normalize(topics))
            if (!_topics.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                AddSuggestion(SuggestionKind.Topic, name);

        foreach (var name in Normalize(people))
            if (!_people.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                AddSuggestion(SuggestionKind.Person, name);

        // Only suggest a mood when none is set — never overwrite an existing (user or accepted) mood.
        if (mood is not null && MoodKey is null)
            AddSuggestion(SuggestionKind.Mood, mood.Key, mood.CustomValue);
    }

    private void AddSuggestion(SuggestionKind kind, string value, string? moodCustomValue = null)
    {
        if (!_suggestions.Any(s => s.Matches(kind, value)))
            _suggestions.Add(new MetadataSuggestion(kind, value, moodCustomValue));
    }

    /// <summary>
    /// Accepts a pending Suggestion: promotes it to metadata with provenance
    /// <see cref="MetadataProvenance.AiSuggested"/> (never duplicating or overwriting UserSet metadata)
    /// and removes it from the pending list. Returns false when no such Suggestion exists.
    /// </summary>
    public bool AcceptSuggestion(SuggestionKind kind, string value)
    {
        var suggestion = _suggestions.FirstOrDefault(s => s.Matches(kind, value));
        if (suggestion is null)
            return false;

        switch (kind)
        {
            case SuggestionKind.Topic:
                if (!_topics.Any(t => t.Name.Equals(suggestion.Value, StringComparison.OrdinalIgnoreCase)))
                    _topics.Add(new SessionTopic(suggestion.Value, MetadataProvenance.AiSuggested));
                break;
            case SuggestionKind.Person:
                if (!_people.Any(p => p.Name.Equals(suggestion.Value, StringComparison.OrdinalIgnoreCase)))
                    _people.Add(new SessionPerson(suggestion.Value, MetadataProvenance.AiSuggested));
                break;
            case SuggestionKind.Mood:
                // Never overwrite an existing mood (UserSet wins; CONTEXT.md).
                if (MoodKey is null && Mood.TryOf(suggestion.Value, suggestion.MoodCustomValue, out var accepted))
                    SetMood(accepted);
                break;
        }

        _suggestions.Remove(suggestion);
        return true;
    }

    /// <summary>Rejects a pending Suggestion: removes it without promoting. Returns false when not found.</summary>
    public bool RejectSuggestion(SuggestionKind kind, string value)
    {
        var suggestion = _suggestions.FirstOrDefault(s => s.Matches(kind, value));
        if (suggestion is null)
            return false;

        _suggestions.Remove(suggestion);
        return true;
    }
}
