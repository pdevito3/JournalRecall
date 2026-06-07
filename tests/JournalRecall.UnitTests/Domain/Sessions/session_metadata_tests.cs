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
        s.ReplaceAiSuggestions(["work"], []);
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
    public void setting_moods_resolves_dedupes_and_allows_multiple_customs()
    {
        var s = New();
        // "joyful" resolves to the known "Joyful" (so the custom "joyful" collapses into it); two distinct
        // customs are kept; blanks dropped.
        s.SetMoods(["Joyful", "joyful", "bittersweet", "wired", "  "]);
        s.Moods.ShouldBe(["Joyful", "bittersweet", "wired"], ignoreOrder: true);

        s.SetMoods([]); // replace-all clears
        s.Moods.ShouldBeEmpty();
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
    public void reconciling_people_takes_the_union_of_raw_and_cleaned_mentions()
    {
        var sam = Guid.CreateVersion7();
        var alex = Guid.CreateVersion7();
        var jordan = Guid.CreateVersion7();

        var s = New();
        s.SaveDraft(DocWithMentions((sam, "Sam"), (alex, "Alex")));
        s.BeginCleanup();
        // Cleaned drops Alex but keeps Sam and adds Jordan — the union is all three.
        s.CompleteCleanup(DocWithMentions((sam, "Sam"), (jordan, "Jordan")), "recap");

        s.ReconcileMentionedPeople();

        s.People.Select(p => p.PersonId).ShouldBe([sam, alex, jordan], ignoreOrder: true);
    }

    [Fact]
    public void a_mention_kept_in_only_one_copy_keeps_the_person()
    {
        var sam = Guid.CreateVersion7();

        var s = New();
        s.SaveDraft(DocWithMentions((sam, "Sam"))); // Raw mentions Sam
        s.BeginCleanup();
        s.CompleteCleanup(Doc("polished, no mentions"), "recap"); // Cleaned drops the mention

        s.ReconcileMentionedPeople();

        s.People.Select(p => p.PersonId).ShouldBe([sam]); // union semantics keep Sam
    }

    [Fact]
    public void removing_a_mention_from_both_copies_untags_the_person()
    {
        var sam = Guid.CreateVersion7();

        var s = New();
        s.SaveDraft(DocWithMentions((sam, "Sam")));
        s.ReconcileMentionedPeople();
        s.People.Select(p => p.PersonId).ShouldBe([sam]);

        s.SaveDraft(Doc("the mention is gone now")); // edited away in Raw, never in Cleaned
        s.ReconcileMentionedPeople();

        s.People.ShouldBeEmpty();
    }

    [Fact]
    public void ai_suggestions_never_duplicate_existing_metadata_but_may_add_more_moods()
    {
        var s = New();
        s.SetUserTopics(["work"]);
        s.SetMoods(["Joyful"]);

        // Re-suggesting "work"/"Joyful" is suppressed, but a different mood IS suggested even though one is
        // already set (the "only if none" guard is gone, PRD-0006). People no longer flow through here.
        s.ReplaceAiSuggestions(["work", "travel"], [Mood.Resolve("Joyful"), Mood.Resolve("Content")]);

        s.Suggestions.ShouldContain(g => g.Kind == SuggestionKind.Topic && g.Value == "travel");
        s.Suggestions.ShouldNotContain(g => g.Value == "work");
        s.Suggestions.ShouldContain(g => g.Kind == SuggestionKind.Mood && g.Value == "Content");
        s.Suggestions.ShouldNotContain(g => g.Kind == SuggestionKind.Mood && g.Value == "Joyful"); // already set
    }

    [Fact]
    public void accepting_a_suggestion_promotes_it_and_removes_it_from_the_pending_list()
    {
        var s = New();
        s.ReplaceAiSuggestions(["travel"], []);

        s.AcceptSuggestion(SuggestionKind.Topic, "travel").ShouldBeTrue();

        s.Topics.Select(t => t.Name).ShouldContain("travel");
        s.Suggestions.ShouldBeEmpty();
    }

    [Fact]
    public void rejecting_a_suggestion_drops_it_without_promoting()
    {
        var s = New();
        s.ReplaceAiSuggestions(["travel"], []);

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
    public void accepting_a_mood_suggestion_adds_it_to_the_set()
    {
        var s = New();
        s.SetMoods(["Tired"]);
        s.ReplaceAiSuggestions([], [Mood.Resolve("Content")]);

        s.AcceptSuggestion(SuggestionKind.Mood, "Content").ShouldBeTrue();

        s.Moods.ShouldBe(["Tired", "Content"], ignoreOrder: true); // added, not replaced
        s.Suggestions.ShouldBeEmpty();
    }
}
