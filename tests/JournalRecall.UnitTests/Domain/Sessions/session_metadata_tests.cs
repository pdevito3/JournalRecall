using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Domain.Sessions.Metadata;
using JournalRecall.SharedTestHelpers.Fakes.Sessions;
using static JournalRecall.SharedTestHelpers.Fakes.Sessions.ContentDoc;

namespace JournalRecall.UnitTests.Domain.Sessions;

/// <summary>
/// Unit tests for the <see cref="Session"/> aggregate's Raw revisioning, hand-edit flag, and the
/// metadata/Suggestion rules — the cases complementary to the cleanup state machine
/// (<see cref="session_cleanup_state_machine_tests"/>). No host or DB.
/// </summary>
public class session_metadata_tests
{
    private static Session New() => Session.Create(Guid.CreateVersion7());

    [Fact]
    public void saving_raw_appends_a_revision_only_when_the_text_actually_changes()
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
    public void saving_null_raw_is_treated_as_empty()
    {
        var s = New();
        s.SaveDraft(null!);
        s.RawDraft.ShouldBeEmpty();
        s.LatestRawRevisionNumber.ShouldBe(0); // empty == the empty latest content → nothing minted
    }

    [Fact]
    public void hand_editing_the_cleaned_copy_appends_a_revision_and_flags_it_only_on_change()
    {
        var s = new FakeSessionBuilder().Cleaned("v1").Build();

        s.EditCleaned(Doc("v1")).ShouldBeFalse(); // unchanged (same canonical JSON)
        s.CleanedHasHandEdits.ShouldBeFalse();
        s.CleanedRevisions.Count.ShouldBe(1);

        s.EditCleaned(Doc("v2")).ShouldBeTrue();
        s.CleanedHasHandEdits.ShouldBeTrue();
        PlainText(s.CleanedDraft).ShouldBe("v2");
        s.CleanedRevisions.Count.ShouldBe(2);
    }

    [Fact]
    public void setting_user_topics_replaces_prior_user_topics_but_keeps_ai_ones_and_dedupes()
    {
        var s = New();
        s.ReplaceAiSuggestions(["work"], null);
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
    public void setting_and_clearing_mood()
    {
        var s = New();
        s.SetMood(Mood.Of("Joyful"));
        s.MoodKey.ShouldBe("Joyful");
        s.Mood!.Key.ShouldBe("Joyful");

        s.SetMood(null);
        s.MoodKey.ShouldBeNull();
        s.Mood.ShouldBeNull();
    }

    [Fact]
    public void setting_user_people_replaces_references_by_id_and_dedupes()
    {
        var s = New();
        var sam = Guid.CreateVersion7();
        var alex = Guid.CreateVersion7();

        s.SetUserPeople([sam, alex, sam]); // duplicate id collapses
        s.People.Select(p => p.PersonId).ShouldBe([sam, alex], ignoreOrder: true);

        s.SetUserPeople([alex]); // replace-all: People carry no provenance now
        s.People.Select(p => p.PersonId).ShouldBe([alex]);
    }

    [Fact]
    public void ai_suggestions_never_duplicate_existing_metadata()
    {
        var s = New();
        s.SetUserTopics(["work"]);
        s.SetMood(Mood.Of("Joyful"));

        // Re-suggesting "work" and a mood is suppressed; only the genuinely new topic remains. People no
        // longer flow through the shared suggestion machinery (people-proposal flow, RICH-009).
        s.ReplaceAiSuggestions(["work", "travel"], Mood.Of("Content"));

        s.Suggestions.ShouldContain(g => g.Kind == SuggestionKind.Topic && g.Value == "travel");
        s.Suggestions.ShouldNotContain(g => g.Value == "work");
        s.Suggestions.ShouldNotContain(g => g.Kind == SuggestionKind.Mood); // a mood is already set
    }

    [Fact]
    public void accepting_a_suggestion_promotes_it_and_removes_it_from_the_pending_list()
    {
        var s = New();
        s.ReplaceAiSuggestions(["travel"], null);

        s.AcceptSuggestion(SuggestionKind.Topic, "travel").ShouldBeTrue();

        s.Topics.Select(t => t.Name).ShouldContain("travel");
        s.Suggestions.ShouldBeEmpty();
    }

    [Fact]
    public void rejecting_a_suggestion_drops_it_without_promoting()
    {
        var s = New();
        s.ReplaceAiSuggestions(["travel"], null);

        s.RejectSuggestion(SuggestionKind.Topic, "travel").ShouldBeTrue();

        s.Topics.ShouldBeEmpty();
        s.Suggestions.ShouldBeEmpty();
    }

    [Fact]
    public void responding_to_an_unknown_suggestion_returns_false()
    {
        var s = New();
        s.AcceptSuggestion(SuggestionKind.Topic, "nope").ShouldBeFalse();
        s.RejectSuggestion(SuggestionKind.Person, "nope").ShouldBeFalse();
    }

    [Fact]
    public void accepting_a_custom_mood_suggestion_sets_the_mood_with_its_text()
    {
        var s = New();
        s.ReplaceAiSuggestions([], Mood.Custom("wistful"));

        s.AcceptSuggestion(SuggestionKind.Mood, "Custom").ShouldBeTrue();

        s.Mood!.IsCustom.ShouldBeTrue();
        s.Mood.CustomValue.ShouldBe("wistful");
    }
}
