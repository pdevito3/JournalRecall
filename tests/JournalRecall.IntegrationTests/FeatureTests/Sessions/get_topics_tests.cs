using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.Api.Domain.Sessions.Features;

namespace JournalRecall.IntegrationTests.FeatureTests.Sessions;

/// <summary>
/// GET /topics (PRD-0006, RICH-011) at the integration layer: returns the current User's distinct Topic
/// names across their Sessions (no Topic entity — owned SessionTopic strings), and one User's Topics are
/// never visible to another. Driven through the MediatR slice, no HTTP.
/// </summary>
public class get_topics_tests : TestBase
{
    private async Task SessionWithTopics(TestingServiceScope scope, params string[] topics)
    {
        var id = (await scope.SendAsync(new CreateSession.Command(null, null)))!.Id;
        await scope.SendAsync(new UpdateMetadata.Command(id, new MetadataForWrite(topics, [], "None")));
    }

    [Fact]
    public async Task returns_the_distinct_topic_names_sorted()
    {
        using var scope = new TestingServiceScope();
        await SessionWithTopics(scope, "work", "travel");
        await SessionWithTopics(scope, "work", "home"); // "work" repeats across Sessions

        var topics = await scope.SendAsync(new GetTopics.Query());

        topics.ShouldBe(["home", "travel", "work"]); // distinct + sorted
    }

    [Fact]
    public async Task topics_are_scoped_to_the_owning_user()
    {
        using var alice = new TestingServiceScope();
        await SessionWithTopics(alice, "secret-topic");

        using var bob = new TestingServiceScope();
        await SessionWithTopics(bob, "bob-topic");

        (await bob.SendAsync(new GetTopics.Query())).ShouldBe(["bob-topic"]);
        (await alice.SendAsync(new GetTopics.Query())).ShouldContain("secret-topic");
    }
}
