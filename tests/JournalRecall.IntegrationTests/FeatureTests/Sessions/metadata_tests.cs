using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.Api.Domain.Sessions.Features;
using JournalRecall.Api.Domain.Sessions.Metadata;
using JournalRecall.Api.Exceptions;

namespace JournalRecall.IntegrationTests.FeatureTests.Sessions;

/// <summary>
/// Manual metadata + filtering (issue 0011) at the integration layer: Topics/People/Mood set per-Session
/// with provenance UserSet, the timeline filtered by each via QueryKit, Custom mood round-tripping its
/// free text, an unknown mood rejected, and one User's metadata invisible to another — via the MediatR
/// slice, no HTTP.
/// </summary>
public class metadata_tests : TestBase
{
    private static async Task<Guid> NewSession(TestingServiceScope scope) =>
        (await scope.SendAsync(new CreateSession.Command(null, null))).Id;

    private static Task<UpdateMetadata.Result> SetMetadata(
        TestingServiceScope scope, Guid id, string[] topics, string[] people, MoodDto? mood) =>
        scope.SendAsync(new UpdateMetadata.Command(id, new MetadataForWrite(topics, people, mood)));

    [Fact]
    public async Task a_user_can_set_topics_people_and_mood_including_custom_then_clear()
    {
        using var scope = new TestingServiceScope();
        var id = await NewSession(scope);

        (await SetMetadata(scope, id, ["work", "parenthood"], ["Sam", "Alex"], new MoodDto("Custom", "bittersweet")))
            .ShouldBe(UpdateMetadata.Result.Ok);

        var session = await scope.SendAsync(new GetSession.Query(id));
        session!.Topics.ShouldBe(["work", "parenthood"]);
        session.People.ShouldBe(["Sam", "Alex"], ignoreOrder: true); // labels resolved from the directory
        session.Mood!.Key.ShouldBe("Custom");
        session.Mood.CustomValue.ShouldBe("bittersweet"); // Custom round-trips its free text

        // Removing a topic / clearing the mood is just another set.
        (await SetMetadata(scope, id, ["work"], [], null)).ShouldBe(UpdateMetadata.Result.Ok);
        var updated = await scope.SendAsync(new GetSession.Query(id));
        updated!.Topics.ShouldBe(["work"]);
        updated.People.ShouldBeEmpty();
        updated.Mood.ShouldBeNull();
    }

    [Fact]
    public async Task manually_set_topics_are_userset_and_people_reference_the_directory()
    {
        using var scope = new TestingServiceScope();
        var id = await NewSession(scope);
        await SetMetadata(scope, id, ["work"], ["Sam"], null);

        var session = await scope.ExecuteDbContextAsync(db => db.Sessions
            .IgnoreQueryFilters().Include(s => s.Topics).Include(s => s.People)
            .FirstAsync(s => s.Id == id));

        session.Topics.ShouldAllBe(t => t.Provenance == MetadataProvenance.UserSet);

        // People are directory references now (no name string, no provenance): the SessionPerson points
        // at a Person the manual edit find-or-created in the User's directory.
        var personId = session.People.ShouldHaveSingleItem().PersonId;
        var person = await scope.ExecuteDbContextAsync(db => db.People
            .IgnoreQueryFilters().FirstAsync(p => p.Id == personId));
        person.Label.ShouldBe("Sam");
    }

    [Fact]
    public async Task the_timeline_can_be_filtered_by_topic_and_mood()
    {
        using var scope = new TestingServiceScope();
        var work = await NewSession(scope);
        var travel = await NewSession(scope);
        await SetMetadata(scope, work, ["work"], ["Sam"], new MoodDto("Joyful", null));
        await SetMetadata(scope, travel, ["travel"], ["Alex"], new MoodDto("Tired", null));

        // No `people` name filter: People are directory references now; a PersonId filter is a future slice.
        (await scope.SendAsync(new GetSessionList.Query("topics == \"work\""))).Select(s => s.Id).ShouldBe([work]);
        (await scope.SendAsync(new GetSessionList.Query("mood == \"Joyful\""))).Select(s => s.Id).ShouldBe([work]);
        (await scope.SendAsync(new GetSessionList.Query(null))).Count.ShouldBe(2);

        // The People labels still surface on the timeline rows for display.
        (await scope.SendAsync(new GetSessionList.Query(null))).Single(s => s.Id == work).People.ShouldBe(["Sam"]);
    }

    [Fact]
    public async Task another_users_metadata_is_not_visible_or_filterable_or_writable()
    {
        using var alice = new TestingServiceScope();
        var aliceSession = await NewSession(alice);
        await SetMetadata(alice, aliceSession, ["secret-project"], [], null);

        using var bob = new TestingServiceScope();
        var bobSession = await NewSession(bob);
        await SetMetadata(bob, bobSession, ["secret-project"], [], null);

        // Bob filtering by the shared topic name sees only his own Session.
        (await bob.SendAsync(new GetSessionList.Query("topics == \"secret-project\""))).Select(s => s.Id)
            .ShouldBe([bobSession]);

        // Bob cannot set metadata on Alice's Session (it doesn't exist for him).
        (await SetMetadata(bob, aliceSession, ["x"], [], null)).ShouldBe(UpdateMetadata.Result.NotFound);
    }

    [Fact]
    public async Task an_unknown_mood_is_rejected()
    {
        using var scope = new TestingServiceScope();
        var id = await NewSession(scope);

        // An unknown mood throws out of the slice (→ 422 at the HTTP edge), it is not a Result value.
        await Should.ThrowAsync<InvalidSmartEnumPropertyName>(() =>
            SetMetadata(scope, id, [], [], new MoodDto("Ecstatic", null)));
    }
}
