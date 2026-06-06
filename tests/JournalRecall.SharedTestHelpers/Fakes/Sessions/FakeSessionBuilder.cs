using Bogus;
using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Domain.Sessions.Metadata;

namespace JournalRecall.SharedTestHelpers.Fakes.Sessions;

/// <summary>
/// Arranges a <see cref="Session"/> in any lifecycle state in one line (PRD-0003). Free-text/random
/// fields are defaulted from Bogus; identity is set explicitly via <see cref="WithUserId"/> so a Session
/// ties to its test's User. Build calls the <b>real</b> domain factory and mutators, so the result can
/// only ever be a state the domain actually permits.
/// </summary>
public class FakeSessionBuilder
{
    private static readonly Faker Faker = new();

    private Guid _userId = Guid.CreateVersion7();
    private string? _rawText;
    private Location? _location;
    private readonly List<string> _userTopics = [];
    private readonly List<Guid> _userPeople = [];
    private readonly List<string> _moods = [];
    private bool _cleaned;
    private string? _cleanedText;
    private string _synopsis = "A short recap of the session.";
    private bool _failed;
    private bool _stale;
    private string? _handEditedText;

    /// <summary>Ties this Session to a specific User (the test's tenant). Required for tenant-scoped tests.</summary>
    public FakeSessionBuilder WithUserId(Guid userId) { _userId = userId; return this; }

    public FakeSessionBuilder WithRawText(string rawText) { _rawText = rawText; return this; }

    public FakeSessionBuilder WithLocation(Location location) { _location = location; return this; }

    public FakeSessionBuilder WithLocation(double latitude, double longitude)
    {
        Location.TryCreate(latitude, longitude, out var location);
        _location = location;
        return this;
    }

    public FakeSessionBuilder WithUserTopics(params string[] names) { _userTopics.AddRange(names); return this; }

    /// <summary>References directory People by id (People are directory references now, PRD-0006).</summary>
    public FakeSessionBuilder WithUserPeople(params Guid[] personIds) { _userPeople.AddRange(personIds); return this; }

    /// <summary>Sets the Session's Moods (known mood names or custom text); resolved + deduped on build.</summary>
    public FakeSessionBuilder WithMoods(params string[] moods) { _moods.AddRange(moods); return this; }

    /// <summary>A Session that has been run through a successful Cleanup (status Clean).</summary>
    public FakeSessionBuilder Cleaned(string? cleanedText = null)
    {
        _cleaned = true;
        _cleanedText = cleanedText;
        return this;
    }

    /// <summary>A cleaned Session whose Raw has since advanced — its effective status reads Stale.</summary>
    public FakeSessionBuilder Stale()
    {
        _cleaned = true;
        _stale = true;
        return this;
    }

    /// <summary>A Session whose latest Cleanup run failed (status Failed).</summary>
    public FakeSessionBuilder Failed()
    {
        _cleaned = true;
        _failed = true;
        return this;
    }

    /// <summary>A cleaned Session carrying a user hand-edit on the Cleaned copy (sets the hand-edits flag).</summary>
    public FakeSessionBuilder WithHandEdit(string? cleanedText = null)
    {
        _cleaned = true;
        _handEditedText = cleanedText ?? Faker.Lorem.Sentence();
        return this;
    }

    public Session Build()
    {
        var raw = _rawText ?? Faker.Lorem.Paragraph();

        // Content persists as canonical ProseMirror JSON (ADR-0009), so wrap the plain fixture text the
        // way the real editor would — the derived plaintext columns then round-trip back to these words.
        var session = Session.Create(_userId, _location);
        session.SaveDraft(ContentDoc.Doc(raw));

        if (_userTopics.Count > 0) session.SetUserTopics(_userTopics);
        if (_userPeople.Count > 0) session.SetUserPeople(_userPeople);
        if (_moods.Count > 0) session.SetMoods(_moods);

        if (_cleaned)
        {
            session.BeginCleanup();
            if (_failed)
                session.FailCleanup();
            else
                session.CompleteCleanup(ContentDoc.Doc(_cleanedText ?? $"Polished: {raw}"), _synopsis);
        }

        if (_handEditedText is not null)
            session.EditCleaned(ContentDoc.Doc(_handEditedText));

        // Advance Raw past the cleaned revision so EffectiveCleanupStatus derives Stale.
        if (_stale && !_failed)
            session.SaveDraft(ContentDoc.Doc($"{raw}\n\n{Faker.Lorem.Sentence()}"));

        return session;
    }
}
