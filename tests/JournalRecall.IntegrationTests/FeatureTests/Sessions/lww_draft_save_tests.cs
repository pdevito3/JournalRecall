using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Domain.Sessions.Features;
using JournalRecall.Api.Domain.Sessions.Services;
using static JournalRecall.SharedTestHelpers.Fakes.Sessions.ContentDoc;

namespace JournalRecall.IntegrationTests.FeatureTests.Sessions;

/// <summary>
/// Last-write-wins draft saves (ADR-0013, issue 0032) at the integration layer: a replayed offline save
/// based on the current head appends + becomes current; when the head moved, both contenders persist as
/// Revisions and the later clientSavedAt holds the Draft — in both arrival orders — with the plaintext
/// projection and Stale derivation following the winner; and a save without the new fields (the web
/// client) behaves exactly as before.
/// </summary>
public class lww_draft_save_tests : TestBase
{
    private static DateTimeOffset At(int hour, int minute = 0) =>
        new(2026, 1, 1, hour, minute, 0, TimeSpan.Zero);

    /// <summary>A Session whose Raw is a single base Revision (number 1) the contenders edit from.</summary>
    private static async Task<Guid> SessionWithBase(TestingServiceScope scope)
    {
        var id = (await scope.SendAsync(new CreateSession.Command(null, null)))!.Id;
        await scope.SendAsync(new SaveDraft.Command(id, Doc("base words")));
        return id;
    }

    private static Task<string> RawPlainText(TestingServiceScope scope, Guid id) =>
        scope.ExecuteDbContextAsync(db => db.Sessions.AsNoTracking()
            .Where(s => s.Id == id).Select(s => s.RawPlainText).FirstAsync());

    [Fact]
    public async Task a_replayed_save_based_on_the_current_head_appends_a_revision_and_becomes_current()
    {
        using var scope = new TestingServiceScope();
        var id = await SessionWithBase(scope);

        (await scope.SendAsync(new SaveDraft.Command(
            id, Doc("base words, expanded offline"), BaseRevisionNumber: 1, ClientSavedAt: At(13)))).ShouldBeTrue();

        (await scope.SendAsync(new GetRawRevisions.Query(id)))!.Count.ShouldBe(2);
        PlainText((await scope.SendAsync(new GetSession.Query(id)))!.RawDraft).ShouldBe("base words, expanded offline");
    }

    [Fact]
    public async Task when_the_head_moved_the_later_save_is_the_draft_with_the_earlier_arriving_first()
    {
        using var scope = new TestingServiceScope();
        var id = await SessionWithBase(scope);

        await scope.SendAsync(new SaveDraft.Command(id, Doc("earlier contender"), 1, At(12, 30))); // Revision 2
        await scope.SendAsync(new SaveDraft.Command(id, Doc("later contender"), 1, At(13)));       // conflict → Revision 3, wins

        (await scope.SendAsync(new GetRawRevisions.Query(id)))!.Count.ShouldBe(3); // nothing discarded
        PlainText((await scope.SendAsync(new GetRawRevision.Query(id, 2)))!.Content).ShouldBe("earlier contender");
        PlainText((await scope.SendAsync(new GetRawRevision.Query(id, 3)))!.Content).ShouldBe("later contender");
        PlainText((await scope.SendAsync(new GetSession.Query(id)))!.RawDraft).ShouldBe("later contender");
    }

    [Fact]
    public async Task when_the_head_moved_the_later_save_is_the_draft_with_the_later_arriving_first()
    {
        using var scope = new TestingServiceScope();
        var id = await SessionWithBase(scope);

        await scope.SendAsync(new SaveDraft.Command(id, Doc("later contender"), 1, At(13)));       // Revision 2
        await scope.SendAsync(new SaveDraft.Command(id, Doc("earlier contender"), 1, At(12, 30))); // conflict → Revision 3, loses

        (await scope.SendAsync(new GetRawRevisions.Query(id)))!.Count.ShouldBe(3); // the loser is kept too
        PlainText((await scope.SendAsync(new GetRawRevision.Query(id, 3)))!.Content).ShouldBe("earlier contender");
        // Same winner as the other arrival order.
        PlainText((await scope.SendAsync(new GetSession.Query(id)))!.RawDraft).ShouldBe("later contender");
    }

    [Fact]
    public async Task plaintext_projection_and_stale_derivation_follow_the_winner()
    {
        using var scope = new TestingServiceScope();
        var id = await SessionWithBase(scope);
        await scope.GetService<SessionCleanupRunner>().RunAsync(id); // Clean at Revision 1

        await scope.SendAsync(new SaveDraft.Command(id, Doc("later contender"), 1, At(13)));       // Revision 2, wins
        await scope.SendAsync(new SaveDraft.Command(id, Doc("earlier contender"), 1, At(12, 30))); // Revision 3, loses

        var session = await scope.SendAsync(new GetSession.Query(id));
        session!.CleanupStatus.ShouldBe(CleanupStatus.Stale); // the winner advanced past the cleaned Revision
        (await RawPlainText(scope, id)).ShouldBe("later contender"); // search/AI projection reads the winner
    }

    [Fact]
    public async Task a_losing_replay_does_not_flip_a_clean_session_stale()
    {
        using var scope = new TestingServiceScope();
        var id = await SessionWithBase(scope);
        await scope.SendAsync(new SaveDraft.Command(id, Doc("winner words"), 1, At(13))); // Revision 2
        await scope.GetService<SessionCleanupRunner>().RunAsync(id);                      // Clean at Revision 2

        await scope.SendAsync(new SaveDraft.Command(id, Doc("stale offline words"), 1, At(12, 30))); // loses → Revision 3

        var session = await scope.SendAsync(new GetSession.Query(id));
        // Stale follows the winner: the Draft is exactly what was cleaned, so the loser's appended
        // Revision must not cue a re-run.
        session!.CleanupStatus.ShouldBe(CleanupStatus.Clean);
        (await scope.SendAsync(new GetRawRevisions.Query(id)))!.Count.ShouldBe(3);
    }

    [Fact]
    public async Task a_save_without_the_new_fields_still_appends_and_becomes_current_after_a_conflict()
    {
        using var scope = new TestingServiceScope();
        var id = await SessionWithBase(scope);
        await scope.SendAsync(new SaveDraft.Command(id, Doc("later contender"), 1, At(13)));
        await scope.SendAsync(new SaveDraft.Command(id, Doc("earlier contender"), 1, At(12, 30)));

        // The web client sends only rawText — exactly as before LWW existed.
        (await scope.SendAsync(new SaveDraft.Command(id, Doc("typed on the web")))).ShouldBeTrue();

        (await scope.SendAsync(new GetRawRevisions.Query(id)))!.Count.ShouldBe(4);
        PlainText((await scope.SendAsync(new GetSession.Query(id)))!.RawDraft).ShouldBe("typed on the web");
    }
}
