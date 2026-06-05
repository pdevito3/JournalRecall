using Shouldly;
using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Domain.Sessions.Metadata;

namespace JournalRecall.Api.Tests.Domain;

/// <summary>
/// Pure unit tests for the <see cref="Session"/> aggregate's behavior: Raw revisioning, the Cleanup
/// state machine and derived Stale status, hand-edits of the Cleaned copy, and the metadata/Suggestion
/// rules — all without a database.
/// </summary>
public class SessionAggregateTests
{
    private static Session New() => Session.Create(Guid.CreateVersion7());

    private static Session Cleaned(out Session session, string raw = "raw", string cleaned = "cleaned")
    {
        session = New();
        session.SaveDraft(raw);
        session.BeginCleanup();
        session.CompleteCleanup(cleaned, "a synopsis");
        return session;
    }

    [Fact]
    public void A_new_session_is_empty_and_not_run()
    {
        var s = New();
        s.RawDraft.ShouldBeEmpty();
        s.LatestRawRevisionNumber.ShouldBe(0);
        s.CleanupStatus.ShouldBe(CleanupStatus.NotRun);
        s.EffectiveCleanupStatus.ShouldBe(CleanupStatus.NotRun);
    }

    [Fact]
    public void Saving_raw_appends_a_revision_only_when_the_text_actually_changes()
    {
        var s = New();
        s.SaveDraft("hello");
        s.RawDraft.ShouldBe("hello");
        s.LatestRawRevisionNumber.ShouldBe(1);

        s.SaveDraft("hello"); // unchanged — no new revision
        s.LatestRawRevisionNumber.ShouldBe(1);

        s.SaveDraft("hello world");
        s.LatestRawRevisionNumber.ShouldBe(2);
        s.RawRevisions[^1].Content.ShouldBe("hello world");
    }

    [Fact]
    public void Saving_null_raw_is_treated_as_empty()
    {
        var s = New();
        s.SaveDraft(null!);
        s.RawDraft.ShouldBeEmpty();
        s.LatestRawRevisionNumber.ShouldBe(0); // empty == the empty latest content → nothing minted
    }

    [Fact]
    public void A_completed_cleanup_writes_the_cleaned_copy_synopsis_and_a_revision()
    {
        Cleaned(out var s, raw: "helo wrld", cleaned: "Hello world.");

        s.CleanupStatus.ShouldBe(CleanupStatus.Clean);
        s.CleanedDraft.ShouldBe("Hello world.");
        s.Synopsis.ShouldBe("a synopsis");
        s.CleanedRevisions.Count.ShouldBe(1);
        s.CleanedHasHandEdits.ShouldBeFalse();
        s.RawDraft.ShouldBe("helo wrld"); // Raw is never touched
    }

    [Fact]
    public void Editing_raw_after_a_clean_derives_stale_without_changing_the_stored_status()
    {
        Cleaned(out var s);
        s.EffectiveCleanupStatus.ShouldBe(CleanupStatus.Clean);

        s.SaveDraft("raw, revised"); // a newer Raw revision than the one cleaned
        s.EffectiveCleanupStatus.ShouldBe(CleanupStatus.Stale);
        s.CleanupStatus.ShouldBe(CleanupStatus.Clean); // Stale is derived, never persisted
    }

    [Fact]
    public void A_failed_cleanup_records_failure_without_corrupting_a_prior_cleaned_copy()
    {
        Cleaned(out var s, cleaned: "good copy");

        s.SaveDraft("new raw");
        s.BeginCleanup();
        s.FailCleanup();

        s.CleanupStatus.ShouldBe(CleanupStatus.Failed);
        s.CleanedDraft.ShouldBe("good copy");   // prior Cleaned copy intact
        s.CleanedRevisions.Count.ShouldBe(1);    // the failed run appended nothing
    }

    [Fact]
    public void Hand_editing_the_cleaned_copy_appends_a_revision_and_flags_it_only_on_change()
    {
        Cleaned(out var s, cleaned: "v1");

        s.EditCleaned("v1").ShouldBeFalse(); // unchanged
        s.CleanedHasHandEdits.ShouldBeFalse();
        s.CleanedRevisions.Count.ShouldBe(1);

        s.EditCleaned("v2").ShouldBeTrue();
        s.CleanedHasHandEdits.ShouldBeTrue();
        s.CleanedDraft.ShouldBe("v2");
        s.CleanedRevisions.Count.ShouldBe(2);
    }

    [Fact]
    public void A_re_run_supersedes_hand_edits_and_clears_the_flag()
    {
        Cleaned(out var s, cleaned: "v1");
        s.EditCleaned("hand edited");
        s.CleanedHasHandEdits.ShouldBeTrue();

        s.BeginCleanup();
        s.CompleteCleanup("v2 fresh", "syn");

        s.CleanedHasHandEdits.ShouldBeFalse();
        s.CleanedDraft.ShouldBe("v2 fresh");
        s.CleanedRevisions.Count.ShouldBe(3); // v1, hand-edit, v2
    }

    [Fact]
    public void Setting_user_topics_replaces_prior_user_topics_but_keeps_ai_ones_and_dedupes()
    {
        var s = New();
        s.ReplaceAiSuggestions(["work"], [], null);
        s.AcceptSuggestion(SuggestionKind.Topic, "work"); // an AiSuggested topic

        s.SetUserTopics(["Home", " home ", "Travel"]); // trims + de-dupes case-insensitively

        var topics = s.Topics.Select(t => t.Name).ToList();
        topics.ShouldContain("Home");
        topics.ShouldContain("Travel");
        topics.Count(t => t.Equals("home", StringComparison.OrdinalIgnoreCase)).ShouldBe(1);
        topics.ShouldContain("work"); // the AiSuggested topic survives

        s.SetUserTopics([]); // replacing again drops the user topics, Ai one remains
        s.Topics.Select(t => t.Name).ShouldBe(["work"]);
    }

    [Fact]
    public void Setting_and_clearing_mood()
    {
        var s = New();
        s.SetMood(Mood.Of(MoodType.Joyful));
        s.MoodKey.ShouldBe("Joyful");
        s.Mood!.Type.ShouldBe(MoodType.Joyful);

        s.SetMood(null);
        s.MoodKey.ShouldBeNull();
        s.Mood.ShouldBeNull();
    }

    [Fact]
    public void Ai_suggestions_never_duplicate_existing_metadata()
    {
        var s = New();
        s.SetUserTopics(["work"]);
        s.SetUserPeople(["Sam"]);
        s.SetMood(Mood.Of(MoodType.Joyful));

        // Re-suggesting "work"/"Sam" and a mood is suppressed; only the genuinely new items remain.
        s.ReplaceAiSuggestions(["work", "travel"], ["Sam", "Alex"], Mood.Of(MoodType.Content));

        s.Suggestions.ShouldContain(g => g.Kind == SuggestionKind.Topic && g.Value == "travel");
        s.Suggestions.ShouldContain(g => g.Kind == SuggestionKind.Person && g.Value == "Alex");
        s.Suggestions.ShouldNotContain(g => g.Value == "work");
        s.Suggestions.ShouldNotContain(g => g.Kind == SuggestionKind.Mood); // a mood is already set
    }

    [Fact]
    public void Accepting_a_suggestion_promotes_it_and_removes_it_from_the_pending_list()
    {
        var s = New();
        s.ReplaceAiSuggestions(["travel"], [], null);

        s.AcceptSuggestion(SuggestionKind.Topic, "travel").ShouldBeTrue();

        s.Topics.Select(t => t.Name).ShouldContain("travel");
        s.Suggestions.ShouldBeEmpty();
    }

    [Fact]
    public void Rejecting_a_suggestion_drops_it_without_promoting()
    {
        var s = New();
        s.ReplaceAiSuggestions(["travel"], [], null);

        s.RejectSuggestion(SuggestionKind.Topic, "travel").ShouldBeTrue();

        s.Topics.ShouldBeEmpty();
        s.Suggestions.ShouldBeEmpty();
    }

    [Fact]
    public void Responding_to_an_unknown_suggestion_returns_false()
    {
        var s = New();
        s.AcceptSuggestion(SuggestionKind.Topic, "nope").ShouldBeFalse();
        s.RejectSuggestion(SuggestionKind.Person, "nope").ShouldBeFalse();
    }

    [Fact]
    public void Accepting_a_custom_mood_suggestion_sets_the_mood_with_its_text()
    {
        var s = New();
        s.ReplaceAiSuggestions([], [], Mood.Of(MoodType.Custom, "wistful"));

        s.AcceptSuggestion(SuggestionKind.Mood, "Custom").ShouldBeTrue();

        s.Mood!.Type.ShouldBe(MoodType.Custom);
        s.Mood.CustomValue.ShouldBe("wistful");
    }
}
