using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.Api.Domain.Sessions.Features;
using JournalRecall.Api.Domain.Sessions.Metadata;

namespace JournalRecall.IntegrationTests.FeatureTests.Sessions;

/// <summary>
/// Manual metadata + filtering (issue 0011, PRD-0006) at the integration layer: Topics (UserSet) set
/// per-Session, multiple Moods (known + custom), the timeline filtered by topic/mood via QueryKit
/// (any-mood match), and one User's metadata invisible to another — via the MediatR slice, no HTTP.
/// People are not edited here; they project from the prose @-mentions (see <c>people_projection_tests</c>).
/// </summary>
public class metadata_tests : TestBase
{
    private static async Task<Guid> NewSession(TestingServiceScope scope) =>
        (await scope.SendAsync(new CreateSession.Command(null, null))).Id;

    private static Task<UpdateMetadata.Result> SetMetadata(
        TestingServiceScope scope, Guid id, string[] topics, string[] moods, string activity = "None") =>
        scope.SendAsync(new UpdateMetadata.Command(id, new MetadataForWrite(topics, moods, activity)));

    [Fact]
    public async Task a_user_can_set_topics_and_multiple_moods_then_clear()
    {
        using var scope = new TestingServiceScope();
        var id = await NewSession(scope);

        (await SetMetadata(scope, id, ["work", "parenthood"], ["Tired", "bittersweet"]))
            .ShouldBe(UpdateMetadata.Result.Ok);

        var session = await scope.SendAsync(new GetSession.Query(id));
        session!.Topics.ShouldBe(["work", "parenthood"]);
        session.Moods.ShouldBe(["Tired", "bittersweet"], ignoreOrder: true); // known + custom round-trip

        // Removing a topic / clearing the moods is just another set.
        (await SetMetadata(scope, id, ["work"], [])).ShouldBe(UpdateMetadata.Result.Ok);
        var updated = await scope.SendAsync(new GetSession.Query(id));
        updated!.Topics.ShouldBe(["work"]);
        updated.Moods.ShouldBeEmpty();
    }

    [Fact]
    public async Task manually_set_topics_are_userset()
    {
        using var scope = new TestingServiceScope();
        var id = await NewSession(scope);
        await SetMetadata(scope, id, ["work"], []);

        var session = await scope.ExecuteDbContextAsync(db => db.Sessions
            .IgnoreQueryFilters().Include(s => s.Topics)
            .FirstAsync(s => s.Id == id));

        session.Topics.ShouldAllBe(t => t.Provenance == MetadataProvenance.UserSet);
    }

    [Fact]
    public async Task the_timeline_filters_by_topic_and_by_any_matching_mood()
    {
        using var scope = new TestingServiceScope();
        var work = await NewSession(scope);
        var travel = await NewSession(scope);
        await SetMetadata(scope, work, ["work"], ["Joyful", "Tired"]); // two moods
        await SetMetadata(scope, travel, ["travel"], ["Calm"]);

        (await scope.SendAsync(new GetSessionList.Query("topics == \"work\""))).Select(s => s.Id).ShouldBe([work]);
        // Any-mood match (case-insensitive resolution): the work Session matches on either of its moods.
        (await scope.SendAsync(new GetSessionList.Query(null, "Joyful"))).Select(s => s.Id).ShouldBe([work]);
        (await scope.SendAsync(new GetSessionList.Query(null, "tired"))).Select(s => s.Id).ShouldBe([work]);
        (await scope.SendAsync(new GetSessionList.Query(null, "Calm"))).Select(s => s.Id).ShouldBe([travel]);
        (await scope.SendAsync(new GetSessionList.Query(null))).Count.ShouldBe(2);

        // Both moods surface on the timeline row for display.
        var workRow = (await scope.SendAsync(new GetSessionList.Query(null))).Single(s => s.Id == work);
        workRow.Moods.ShouldBe(["Joyful", "Tired"], ignoreOrder: true);
    }

    [Fact]
    public async Task another_users_metadata_is_not_visible_or_filterable_or_writable()
    {
        using var alice = new TestingServiceScope();
        var aliceSession = await NewSession(alice);
        await SetMetadata(alice, aliceSession, ["secret-project"], []);

        using var bob = new TestingServiceScope();
        var bobSession = await NewSession(bob);
        await SetMetadata(bob, bobSession, ["secret-project"], []);

        // Bob filtering by the shared topic name sees only his own Session.
        (await bob.SendAsync(new GetSessionList.Query("topics == \"secret-project\""))).Select(s => s.Id)
            .ShouldBe([bobSession]);

        // Bob cannot set metadata on Alice's Session (it doesn't exist for him).
        (await SetMetadata(bob, aliceSession, ["x"], [])).ShouldBe(UpdateMetadata.Result.NotFound);
    }

    [Fact]
    public async Task a_new_session_defaults_to_activity_none()
    {
        using var scope = new TestingServiceScope();
        var id = await NewSession(scope);

        (await scope.SendAsync(new GetSession.Query(id)))!.Activity.ShouldBe("None");
    }

    [Fact]
    public async Task metadata_write_round_trips_activity_and_replaces_all_fields_wholesale()
    {
        using var scope = new TestingServiceScope();
        var id = await NewSession(scope);

        // A known activity round-trips by its canonical name (case-insensitive in → canonical out).
        await SetMetadata(scope, id, ["work"], ["Tired"], "walking");
        var known = await scope.SendAsync(new GetSession.Query(id));
        known!.Activity.ShouldBe("Walking");
        known.Topics.ShouldBe(["work"]);
        known.Moods.ShouldBe(["Tired"]);

        // A custom activity round-trips as its raw free-text, never the literal "Custom".
        await SetMetadata(scope, id, ["work"], ["Tired"], "cooking");
        (await scope.SendAsync(new GetSession.Query(id)))!.Activity.ShouldBe("cooking");

        // Full replace (ADR-0011): a blank activity + empty lists clears everything wholesale — no
        // field is "left alone". None is distinct from a prior Walking/cooking choice.
        await SetMetadata(scope, id, [], [], "");
        var cleared = await scope.SendAsync(new GetSession.Query(id));
        cleared!.Activity.ShouldBe("None");
        cleared.Topics.ShouldBeEmpty();
        cleared.Moods.ShouldBeEmpty();
    }

    [Fact]
    public async Task any_free_text_is_accepted_as_a_custom_mood()
    {
        using var scope = new TestingServiceScope();
        var id = await NewSession(scope);

        // There are no "invalid" moods anymore: a non-known string is simply a custom mood (PRD-0006).
        (await SetMetadata(scope, id, [], ["Ecstatic"])).ShouldBe(UpdateMetadata.Result.Ok);
        (await scope.SendAsync(new GetSession.Query(id)))!.Moods.ShouldBe(["Ecstatic"]);
    }
}
