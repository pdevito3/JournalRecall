using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Domain.Sessions.Features;
using JournalRecall.Api.Domain.Sessions.Services;
using JournalRecall.SharedTestHelpers.Fakes.Sessions;

namespace JournalRecall.IntegrationTests.FeatureTests.Sessions;

/// <summary>
/// AI Cleanup (issue 0008) at the integration layer: a run appends a Cleaned Revision while leaving Raw
/// and its history untouched, and a later Raw edit derives Stale — driven through the real
/// <see cref="SessionCleanupRunner"/> and the scripted chat client.
/// </summary>
public class cleanup_revision_and_stale_tests : TestBase
{
    [Fact]
    public async Task cleanup_appends_a_cleaned_revision_and_leaves_raw_history_unchanged()
    {
        using var scope = new TestingServiceScope();
        var session = new FakeSessionBuilder().WithUserId(scope.CurrentUserId).WithRawText("helo wrld").Build();
        await scope.InsertAsync(session);

        await scope.GetService<SessionCleanupRunner>().RunAsync(session.Id);

        var cleaned = await scope.SendAsync(new GetCleanedRevisions.Query(session.Id));
        cleaned!.Count.ShouldBe(1);
        (await scope.SendAsync(new GetCleanedRevision.Query(session.Id, 1)))!.Content.ShouldBe("Polished: helo wrld");

        // Raw is byte-for-byte unchanged — still its single original Revision.
        (await scope.SendAsync(new GetRawRevisions.Query(session.Id)))!.Count.ShouldBe(1);
    }

    [Fact]
    public async Task editing_raw_after_cleanup_flips_status_to_stale()
    {
        using var scope = new TestingServiceScope();
        var session = new FakeSessionBuilder().WithUserId(scope.CurrentUserId).WithRawText("first draft").Build();
        await scope.InsertAsync(session);

        await scope.GetService<SessionCleanupRunner>().RunAsync(session.Id);
        (await scope.SendAsync(new GetSession.Query(session.Id)))!.CleanupStatus.ShouldBe(CleanupStatus.Clean);

        await scope.SendAsync(new SaveDraft.Command(session.Id, "first draft, now revised"));

        (await scope.SendAsync(new GetSession.Query(session.Id)))!.CleanupStatus.ShouldBe(CleanupStatus.Stale);
    }
}
