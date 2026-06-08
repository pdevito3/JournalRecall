using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.Api.Domain.Sessions.Features;
using JournalRecall.Api.Domain.Sessions.Metadata;
using JournalRecall.Api.Domain.Sessions.Services;
using JournalRecall.SharedTestHelpers.Fakes.Sessions;

namespace JournalRecall.IntegrationTests.FeatureTests.Sessions;

/// <summary>
/// AI metadata Suggestions (issue 0012) at the integration layer: a Cleanup run proposes Topic/Mood
/// Suggestions distinct from accepted metadata; accept promotes them (provenance AiSuggested) and reject
/// discards them; UserSet metadata is never overwritten or duplicated; suggestions are per-User. People no
/// longer flow through this shared machinery (people-proposal flow, RICH-009). Driven through the runner +
/// scripted client and RespondToSuggestion, no HTTP.
/// </summary>
public class cleanup_suggestion_tests : TestBase
{
    private async Task<Guid> CleanedSessionWithRaw(TestingServiceScope scope, string raw)
    {
        var session = new FakeSessionBuilder().WithUserId(scope.CurrentUserId).WithRawText(raw).Build();
        await scope.InsertAsync(session);
        await scope.GetService<SessionCleanupRunner>().RunAsync(session.Id);
        return session.Id;
    }

    [Fact]
    public async Task a_cleanup_run_yields_suggestions_distinct_from_accepted_metadata()
    {
        using var scope = new TestingServiceScope();
        CleanupChat.SuggestTopics = ["work"];
        CleanupChat.SuggestPeople = ["Sam"]; // carried in peopleProposal, but not as a shared Suggestion
        CleanupChat.SuggestMood = "Joyful";
        var id = await CleanedSessionWithRaw(scope, "had a great day at work with Sam");

        var session = await scope.SendAsync(new GetSession.Query(id));
        session!.Topics.ShouldBeEmpty();   // nothing accepted yet
        session.People.ShouldBeEmpty();
        session.Moods.ShouldBeEmpty();
        // Only Topic + Mood flow through MetadataSuggestion now — People left for the proposal flow (RICH-009).
        session.Suggestions.Select(s => (s.Kind, s.Value)).ShouldBe(
            [(SuggestionKind.Topic, "work"), (SuggestionKind.Mood, "Joyful")], ignoreOrder: true);
    }

    [Fact]
    public async Task accepting_a_suggestion_promotes_it_with_provenance_aisuggested_and_removes_it()
    {
        using var scope = new TestingServiceScope();
        CleanupChat.SuggestTopics = ["work"];
        var id = await CleanedSessionWithRaw(scope, "work stuff");

        (await scope.SendAsync(new RespondToSuggestion.Command(id, SuggestionKind.Topic, "work", Accept: true)))
            .ShouldBeTrue();

        var session = await scope.SendAsync(new GetSession.Query(id));
        session!.Topics.ShouldBe(["work"]);
        session.Suggestions.ShouldBeEmpty();

        var entity = await scope.ExecuteDbContextAsync(db => db.Sessions
            .IgnoreQueryFilters().Include(s => s.Topics).FirstAsync(s => s.Id == id));
        entity.Topics.Single(t => t.Name == "work").Provenance.ShouldBe(MetadataProvenance.AiSuggested);
    }

    [Fact]
    public async Task rejecting_a_suggestion_removes_it_without_promoting()
    {
        using var scope = new TestingServiceScope();
        CleanupChat.SuggestMood = "Calm";
        var id = await CleanedSessionWithRaw(scope, "a quiet evening");

        (await scope.SendAsync(new RespondToSuggestion.Command(id, SuggestionKind.Mood, "Calm", Accept: false)))
            .ShouldBeTrue();

        var session = await scope.SendAsync(new GetSession.Query(id));
        session!.Moods.ShouldBeEmpty();
        session.Suggestions.ShouldBeEmpty();
    }

    [Fact]
    public async Task ai_never_overwrites_or_duplicates_userset_metadata()
    {
        using var scope = new TestingServiceScope();
        var session = new FakeSessionBuilder().WithUserId(scope.CurrentUserId)
            .WithRawText("work day, felt sad, saw Sam").Build();
        await scope.InsertAsync(session);
        // The user has already set a Topic and a Mood themselves.
        await scope.SendAsync(new UpdateMetadata.Command(session.Id,
            new MetadataForWrite(["work"], ["Sad"], "None")));

        // AI suggests the same topic + a new one, the same mood + a new one.
        CleanupChat.SuggestTopics = ["work", "travel"];
        CleanupChat.SuggestMoods = ["Sad", "Joyful"];
        await scope.GetService<SessionCleanupRunner>().RunAsync(session.Id);

        var view = await scope.SendAsync(new GetSession.Query(session.Id));
        // The already-set "work" is not re-suggested; only the new "travel" is.
        view!.Suggestions.Where(s => s.Kind == SuggestionKind.Topic).Select(s => s.Value).ShouldBe(["travel"]);
        // The user's mood is untouched; "Sad" is not re-suggested, but a new "Joyful" is (guard dropped).
        view.Moods.ShouldBe(["Sad"]);
        view.Suggestions.Where(s => s.Kind == SuggestionKind.Mood).Select(s => s.Value).ShouldBe(["Joyful"]);
        // The user's Topic remains a single entry (no duplicate).
        view.Topics.ShouldBe(["work"]);
    }

    [Fact]
    public async Task suggestions_are_scoped_to_the_owning_user()
    {
        using var alice = new TestingServiceScope();
        CleanupChat.SuggestTopics = ["work"];
        var id = await CleanedSessionWithRaw(alice, "work");

        using var bob = new TestingServiceScope();
        (await bob.SendAsync(new RespondToSuggestion.Command(id, SuggestionKind.Topic, "work", Accept: true)))
            .ShouldBeFalse();
        (await bob.SendAsync(new RespondToSuggestion.Command(id, SuggestionKind.Topic, "work", Accept: false)))
            .ShouldBeFalse();

        // Alice's suggestion is still pending.
        (await alice.SendAsync(new GetSession.Query(id)))!.Suggestions
            .ShouldContain(s => s.Kind == SuggestionKind.Topic && s.Value == "work");
    }
}
