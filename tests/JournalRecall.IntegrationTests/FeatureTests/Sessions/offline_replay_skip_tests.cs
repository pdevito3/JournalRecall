using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.Api.Domain.Sessions.Features;
using JournalRecall.SharedTestHelpers.Fakes.Sessions;
using static JournalRecall.SharedTestHelpers.Fakes.Sessions.ContentDoc;

namespace JournalRecall.IntegrationTests.FeatureTests.Sessions;

/// <summary>
/// The skip-if-older rule for the Session's user-owned full-replace writes (ADR-0013, issue 0032): a
/// queued offline metadata or Cleaned hand-edit whose clientSavedAt is older than the Session's last
/// write is acknowledged but not applied, so it can't clobber newer server state; a newer or
/// timestamp-less write (the web client) applies exactly as before.
/// </summary>
public class offline_replay_skip_tests : TestBase
{
    private static DateTimeOffset At(int hour, int minute = 0) =>
        new(2026, 1, 1, hour, minute, 0, TimeSpan.Zero);

    private static MetadataForWrite Metadata(string topic, DateTimeOffset? clientSavedAt = null) =>
        new([topic], [], "None", clientSavedAt);

    [Fact]
    public async Task a_metadata_write_saved_before_the_sessions_last_write_is_skipped()
    {
        using var scope = new TestingServiceScope();
        var id = (await scope.SendAsync(new CreateSession.Command(null, null)))!.Id; // last write: 12:00
        await scope.SendAsync(new UpdateMetadata.Command(id, Metadata("work")));

        // Saved offline at 11:00 — before the Session's last write → acknowledged, not applied.
        var result = await scope.SendAsync(new UpdateMetadata.Command(id, Metadata("clobber", At(11))));

        result.ShouldBe(UpdateMetadata.Result.Ok);
        (await scope.SendAsync(new GetSession.Query(id)))!.Topics.ShouldBe(["work"]);
    }

    [Fact]
    public async Task a_metadata_write_saved_after_the_sessions_last_write_is_applied()
    {
        using var scope = new TestingServiceScope();
        var id = (await scope.SendAsync(new CreateSession.Command(null, null)))!.Id; // last write: 12:00
        await scope.SendAsync(new UpdateMetadata.Command(id, Metadata("work")));

        await scope.SendAsync(new UpdateMetadata.Command(id, Metadata("newer", At(13))));

        (await scope.SendAsync(new GetSession.Query(id)))!.Topics.ShouldBe(["newer"]);
    }

    [Fact]
    public async Task a_cleaned_hand_edit_saved_before_the_sessions_last_write_is_skipped()
    {
        using var scope = new TestingServiceScope();
        var session = new FakeSessionBuilder().WithUserId(scope.CurrentUserId)
            .WithRawText("raw words").Cleaned("polished words").Build();
        await scope.InsertAsync(session); // last write: 12:00

        (await scope.SendAsync(new SaveCleaned.Command(session.Id, Doc("offline edit"), At(11)))).ShouldBeTrue();

        var after = await scope.SendAsync(new GetSession.Query(session.Id));
        PlainText(after!.CleanedDraft).ShouldBe("polished words"); // untouched
        after.CleanedHasHandEdits.ShouldBeFalse();
        (await scope.SendAsync(new GetCleanedRevisions.Query(session.Id)))!.Count.ShouldBe(1);
    }

    [Fact]
    public async Task a_cleaned_hand_edit_saved_after_the_sessions_last_write_is_applied()
    {
        using var scope = new TestingServiceScope();
        var session = new FakeSessionBuilder().WithUserId(scope.CurrentUserId)
            .WithRawText("raw words").Cleaned("polished words").Build();
        await scope.InsertAsync(session); // last write: 12:00

        await scope.SendAsync(new SaveCleaned.Command(session.Id, Doc("a newer hand edit"), At(13)));

        var after = await scope.SendAsync(new GetSession.Query(session.Id));
        PlainText(after!.CleanedDraft).ShouldBe("a newer hand edit");
        after.CleanedHasHandEdits.ShouldBeTrue();
    }
}
