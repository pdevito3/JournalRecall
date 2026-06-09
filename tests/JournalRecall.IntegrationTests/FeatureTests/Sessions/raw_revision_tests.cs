using JournalRecall.Api.Domain.Sessions.Features;

namespace JournalRecall.IntegrationTests.FeatureTests.Sessions;

/// <summary>
/// Raw Revision history (issue 0005) at the integration layer: each changed save appends an immutable
/// Revision, an unchanged save mints nothing, and another User's revisions are never visible — all via
/// the MediatR slice with no HTTP.
/// </summary>
public class raw_revision_tests : TestBase
{
    private static async Task<Guid> NewSession(TestingServiceScope scope) =>
        (await scope.SendAsync(new CreateSession.Command(null, null)))!.Id;

    [Fact]
    public async Task each_changed_save_appends_a_revision_and_prior_revisions_are_immutable()
    {
        using var scope = new TestingServiceScope();
        var id = await NewSession(scope);

        (await scope.SendAsync(new SaveDraft.Command(id, "draft one"))).ShouldBeTrue();
        (await scope.SendAsync(new SaveDraft.Command(id, "draft one, expanded"))).ShouldBeTrue();

        var revisions = await scope.SendAsync(new GetRawRevisions.Query(id));
        revisions!.Count.ShouldBe(2);
        revisions.Select(r => r.RevisionNumber).ShouldBe([2, 1]); // newest first

        // The first Revision still holds the original text, unchanged by the later save.
        var first = await scope.SendAsync(new GetRawRevision.Query(id, 1));
        first!.Content.ShouldBe("draft one");
    }

    [Fact]
    public async Task an_unchanged_save_does_not_append_a_revision()
    {
        using var scope = new TestingServiceScope();
        var id = await NewSession(scope);

        await scope.SendAsync(new SaveDraft.Command(id, "same text"));
        await scope.SendAsync(new SaveDraft.Command(id, "same text")); // no content change → no new Revision

        (await scope.SendAsync(new GetRawRevisions.Query(id)))!.Count.ShouldBe(1);
    }

    [Fact]
    public async Task another_user_cannot_read_revisions()
    {
        using var alice = new TestingServiceScope();
        var id = await NewSession(alice);
        await alice.SendAsync(new SaveDraft.Command(id, "private words"));

        using var bob = new TestingServiceScope();
        (await bob.SendAsync(new GetRawRevisions.Query(id))).ShouldBeNull(); // Privacy invariant
    }
}
