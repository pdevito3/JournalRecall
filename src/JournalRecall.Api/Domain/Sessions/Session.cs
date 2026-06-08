using JournalRecall.Api.Domain.Sessions.Content;
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
    private readonly List<string> _moods = [];
    private readonly List<MetadataSuggestion> _suggestions = [];
    private readonly List<PersonTagProposal> _peopleProposals = [];

    public Guid UserId { get; private set; }

    /// <summary>
    /// The live, autosaved Raw content as canonical ProseMirror/tiptap JSON (ADR-0009). Human-owned and
    /// stored exactly as the editor serialized it — never mutated by the server. Empty until first typed.
    /// </summary>
    public string RawDraft
    {
        get => field;
        // RawPlainText is derived in lockstep here, so the projection can never drift from the draft.
        private set
        {
            field = value ?? string.Empty;
            RawPlainText = ProseMirrorToPlainText.Render(field);
        }
    } = string.Empty;

    /// <summary>
    /// The derived plaintext projection of <see cref="RawDraft"/> (ADR-0009), recomputed on every save.
    /// This — not the JSON markup — is what the search index and the AI Cleanup input read, so formatting
    /// never hides content from search and AI quality is unaffected by the rich representation.
    /// </summary>
    public string RawPlainText { get; private set; } = string.Empty;

    /// <summary>The optional captured latitude/longitude (CONTEXT.md Location); null unless geo opt-in stamped one.</summary>
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }

    /// <summary>The captured geo-point as a value object, or null when none was stored.</summary>
    public Location? Location => Sessions.Location.TryCreate(Latitude, Longitude, out var location) ? location : null;

    /// <summary>
    /// The AI-derived, polished copy produced by the latest Cleanup (CONTEXT.md), as canonical
    /// ProseMirror/tiptap JSON (ADR-0009). Also user hand-editable. Empty until a run succeeds.
    /// </summary>
    public string CleanedDraft
    {
        get => field;
        // CleanedPlainText is derived in lockstep here, so the projection can never drift from the draft.
        private set
        {
            field = value ?? string.Empty;
            CleanedPlainText = ProseMirrorToPlainText.Render(field);
        }
    } = string.Empty;

    /// <summary>The derived plaintext projection of <see cref="CleanedDraft"/> (ADR-0009), recomputed on every save.</summary>
    public string CleanedPlainText { get; private set; } = string.Empty;

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

    /// <summary>The directory People referenced on this Session, by <see cref="SessionPerson.PersonId"/>.</summary>
    public IReadOnlyList<SessionPerson> People => _people;

    /// <summary>AI-proposed metadata awaiting accept/reject (CONTEXT.md). Distinct from accepted metadata.</summary>
    public IReadOnlyList<MetadataSuggestion> Suggestions => _suggestions;

    /// <summary>
    /// AI-proposed People tags from the last Cleanup awaiting per-Person review (PRD-0006, RICH-009).
    /// Distinct from the shared <see cref="Suggestions"/> chip flow — People are tagged inline in the prose,
    /// so approval inserts mention nodes rather than promoting a chip.
    /// </summary>
    public IReadOnlyList<PersonTagProposal> PeopleProposals => _peopleProposals;

    /// <summary>
    /// The Session's Moods (PRD-0006): canonical mood strings — known mood names or custom text — deduped
    /// case-insensitively. No primary, no ordering, no provenance. Persisted as a JSON column.
    /// </summary>
    public IReadOnlyList<string> Moods => _moods;

    /// <summary>The latest Raw Revision number (== the count, since numbers are sequential). 0 when empty.</summary>
    public int LatestRawRevisionNumber => _rawRevisions.Count;

    /// <summary>
    /// The Cleanup status as the user sees it: the stored status, except a Session that has a prior
    /// successful Cleanup whose Raw has since advanced past it reads as <see cref="CleanupStatus.Stale"/>
    /// (CONTEXT.md — "Stale means the latest Raw Revision is newer than the last successful Cleanup").
    /// This holds for any non-<see cref="CleanupStatus.Running"/> state — so a Clean → Raw edit →
    /// Failed re-run → Raw edit still reads Stale, not Failed. <see cref="CleanupStatus.Running"/> is
    /// never overridden (a run is in flight), and a Failed run with no prior success reads Failed.
    /// </summary>
    public CleanupStatus EffectiveCleanupStatus =>
        CleanupStatus != CleanupStatus.Running
        && LastCleanedRawRevisionNumber > 0
        && LatestRawRevisionNumber > LastCleanedRawRevisionNumber
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

    /// <summary>
    /// Folds in a successful Cleanup whose proposed People await per-Person approval (the default,
    /// PRD-0006/RICH-009): completes the copy + Synopsis, records the AI Topic/Mood Suggestions, and parks
    /// the People-tag <paramref name="proposals"/> for review. The People badges are deliberately left as
    /// they were — nothing is tagged until the User approves each proposal. This is the cohesive
    /// counterpart to <see cref="CompleteCleanupWithInlineMentions"/>: one of the two is always the whole
    /// terminal state, so the proposal-vs-inline invariant can't drift across separate calls.
    /// </summary>
    public void CompleteCleanupWithProposals(
        string cleanedText, string synopsis, IEnumerable<string> topicSuggestions, IEnumerable<Mood> moodSuggestions,
        IEnumerable<PersonTagProposal> proposals)
    {
        CompleteCleanup(cleanedText, synopsis);
        ReplaceAiSuggestions(topicSuggestions, moodSuggestions);
        ReplacePeopleProposals(proposals);
    }

    /// <summary>
    /// Folds in a successful Cleanup whose People were resolved and tagged inline at run time (approval
    /// off, PRD-0006/RICH-009): <paramref name="cleanedText"/> already carries the mention nodes, so no
    /// proposals are pending and the People badges are reconciled straight from the prose. The cohesive
    /// counterpart to <see cref="CompleteCleanupWithProposals"/>.
    /// </summary>
    public void CompleteCleanupWithInlineMentions(
        string cleanedText, string synopsis, IEnumerable<string> topicSuggestions, IEnumerable<Mood> moodSuggestions)
    {
        CompleteCleanup(cleanedText, synopsis);
        ReplaceAiSuggestions(topicSuggestions, moodSuggestions);
        ReplacePeopleProposals([]);
        ReconcileMentionedPeople();
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

    /// <summary>
    /// Replaces the Session's People references with the given directory <paramref name="personIds"/>
    /// (deduped). People carry no provenance (PRD-0006); the caller resolves labels to directory
    /// <see cref="People.Person"/> ids first.
    /// </summary>
    public void SetUserPeople(IEnumerable<Guid> personIds)
    {
        _people.Clear();
        foreach (var personId in (personIds ?? []).Distinct())
            _people.Add(new SessionPerson(personId));
    }

    /// <summary>
    /// Reconciles the Session's People references to the <b>union</b> of the directory People mentioned in
    /// the Raw and Cleaned copies (PRD-0006, RICH-006), so the People badges are a pure projection of the
    /// prose: a mention in either copy keeps the Person; a Person mentioned in neither is dropped. Called at
    /// each save point once the editors produce mention nodes (RICH-007).
    /// </summary>
    public void ReconcileMentionedPeople()
    {
        var mentioned = new HashSet<Guid>(MentionProjection.ExtractPersonIds(RawDraft));
        mentioned.UnionWith(MentionProjection.ExtractPersonIds(CleanedDraft));
        SetUserPeople(mentioned);
    }

    /// <summary>
    /// Replaces the Session's Moods. Each input string is resolved (known-vs-custom) to its canonical form
    /// and the set is deduped case-insensitively; blanks are dropped, multiple customs are allowed.
    /// </summary>
    public void SetMoods(IEnumerable<string> moods)
    {
        _moods.Clear();
        _moods.AddRange(ResolveMoods(moods));
    }

    private static IEnumerable<string> ResolveMoods(IEnumerable<string> moods) => (moods ?? [])
        .Where(m => !string.IsNullOrWhiteSpace(m))
        .Select(m => Mood.Resolve(m).Value)
        .Distinct(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> Normalize(IEnumerable<string> names) => (names ?? [])
        .Select(n => n?.Trim() ?? string.Empty)
        .Where(n => n.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Replaces the pending AI Topic/Mood Suggestions with a fresh set from a Cleanup run. Suggestions
    /// that would duplicate metadata the Session already carries are dropped (AI never duplicates existing
    /// metadata, CONTEXT.md): an existing Topic name, or a Mood already present. Moods can be suggested even
    /// when others are already set (PRD-0006). People no longer flow through this shared machinery — they go
    /// through the people-proposal flow (RICH-009).
    /// </summary>
    public void ReplaceAiSuggestions(IEnumerable<string> topics, IEnumerable<Mood> moods)
    {
        _suggestions.Clear();

        foreach (var name in Normalize(topics))
            if (!_topics.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                AddSuggestion(SuggestionKind.Topic, name);

        // Suggest any Mood not already present — the "only if none set" guard is gone (PRD-0006).
        foreach (var value in (moods ?? []).Select(m => m.Value))
            if (!_moods.Any(m => m.Equals(value, StringComparison.OrdinalIgnoreCase)))
                AddSuggestion(SuggestionKind.Mood, value);
    }

    private void AddSuggestion(SuggestionKind kind, string value)
    {
        if (!_suggestions.Any(s => s.Matches(kind, value)))
            _suggestions.Add(new MetadataSuggestion(kind, value));
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
            case SuggestionKind.Mood:
                // Add the Mood to the set if not already present (PRD-0006 — Moods are multi-valued).
                var mood = Mood.Resolve(suggestion.Value).Value;
                if (!_moods.Any(m => m.Equals(mood, StringComparison.OrdinalIgnoreCase)))
                    _moods.Add(mood);
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

    /// <summary>
    /// Replaces the pending People-tag proposals with a fresh set from a Cleanup run (PRD-0006, RICH-009).
    /// A new run supersedes any prior, un-reviewed proposals.
    /// </summary>
    public void ReplacePeopleProposals(IEnumerable<PersonTagProposal> proposals)
    {
        _peopleProposals.Clear();
        _peopleProposals.AddRange(proposals ?? []);
    }

    /// <summary>Drops a pending People-tag proposal by label (after it is approved or rejected). Returns false when absent.</summary>
    public bool RemovePersonProposal(string label)
    {
        var proposal = _peopleProposals.FirstOrDefault(p => p.Matches(label));
        if (proposal is null)
            return false;

        _peopleProposals.Remove(proposal);
        return true;
    }

    /// <summary>
    /// Applies an approved People-tag insertion (RICH-009): the caller has wrapped the approved spans in
    /// mention nodes (<see cref="MentionInsertion"/>) — this commits the resulting Cleaned copy. When the
    /// copy actually changed a Cleaned Revision is appended (ADR-0003 append-only), and the People badges
    /// are reconciled from the prose. This is an approved AI tag, not a free-form hand-edit, so the
    /// hand-edit flag is left untouched (a re-run warns only about the User's own edits).
    /// </summary>
    public void ApplyCleanedMentions(string cleanedText)
    {
        cleanedText ??= string.Empty;
        if (!string.Equals(cleanedText, CleanedDraft, StringComparison.Ordinal))
        {
            CleanedDraft = cleanedText;
            _cleanedRevisions.Add(new CleanedRevision(_cleanedRevisions.Count + 1, cleanedText));
        }
        ReconcileMentionedPeople();
    }
}
