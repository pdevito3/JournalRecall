using JournalRecall.Api.Domain.Sessions;
using static JournalRecall.SharedTestHelpers.Fakes.Sessions.ContentDoc;

namespace JournalRecall.UnitTests.Domain.Sessions;

/// <summary>
/// Last-write-wins draft saves (ADR-0013, issue 0032) at the domain layer: a save based on the current
/// head appends and becomes current exactly as before; when the head moved, every contender keeps a
/// Revision and the later actual save time holds the Draft slot — in both arrival orders, with nothing
/// ever discarded — and Stale derivation pins to the winning Draft, not the stream head.
/// </summary>
public class session_lww_draft_tests
{
    private static DateTimeOffset At(int hour, int minute = 0) =>
        new(2026, 1, 1, hour, minute, 0, TimeSpan.Zero);

    /// <summary>A Session whose Raw is a single base Revision the contenders both edit from.</summary>
    private static Session WithBase()
    {
        var session = Session.Create(Guid.CreateVersion7());
        session.SaveDraft(Doc("base words"), baseRevisionNumber: null, savedAt: At(9)); // Revision 1
        return session;
    }

    [Fact]
    public void a_save_based_on_the_current_head_appends_and_becomes_current()
    {
        var session = WithBase();

        var changed = session.SaveDraft(Doc("base words, expanded offline"), baseRevisionNumber: 1, savedAt: At(10));

        changed.ShouldBeTrue();
        session.RawRevisions.Count.ShouldBe(2);
        PlainText(session.RawDraft).ShouldBe("base words, expanded offline");
        session.RawDraftRevisionNumber.ShouldBe(2);
        session.RawDraftSavedAt.ShouldBe(At(10));
    }

    [Fact]
    public void when_the_head_moved_the_later_save_wins_the_draft_with_the_earlier_arriving_first()
    {
        var session = WithBase();

        session.SaveDraft(Doc("earlier contender"), baseRevisionNumber: 1, savedAt: At(10)); // Revision 2
        session.SaveDraft(Doc("later contender"), baseRevisionNumber: 1, savedAt: At(11));   // conflict → Revision 3, wins

        session.RawRevisions.Count.ShouldBe(3); // both contenders kept — nothing discarded
        PlainText(session.RawRevisions[1].Content).ShouldBe("earlier contender");
        PlainText(session.RawRevisions[2].Content).ShouldBe("later contender");
        PlainText(session.RawDraft).ShouldBe("later contender");
        session.RawDraftRevisionNumber.ShouldBe(3);
    }

    [Fact]
    public void when_the_head_moved_the_later_save_wins_the_draft_with_the_later_arriving_first()
    {
        var session = WithBase();

        session.SaveDraft(Doc("later contender"), baseRevisionNumber: 1, savedAt: At(11));   // Revision 2
        var changed = session.SaveDraft(Doc("earlier contender"), baseRevisionNumber: 1, savedAt: At(10)); // conflict → loses

        changed.ShouldBeFalse(); // the Draft did not move
        session.RawRevisions.Count.ShouldBe(3); // the loser still appended — nothing discarded
        PlainText(session.RawRevisions[2].Content).ShouldBe("earlier contender");
        PlainText(session.RawDraft).ShouldBe("later contender"); // same winner as the other arrival order
        session.RawDraftRevisionNumber.ShouldBe(2);
        session.RawDraftSavedAt.ShouldBe(At(11));
    }

    [Fact]
    public void a_losing_contender_does_not_flip_a_clean_session_stale()
    {
        var session = WithBase();
        session.SaveDraft(Doc("winner words"), baseRevisionNumber: 1, savedAt: At(11)); // Revision 2
        session.BeginCleanup();
        session.CompleteCleanup(Doc("Polished: winner words"), "synopsis"); // cleaned the winning Draft (rev 2)

        session.SaveDraft(Doc("stale offline words"), baseRevisionNumber: 1, savedAt: At(10)); // loses → Revision 3

        // Stale derivation follows the winner: the Draft is exactly what was cleaned, so still Clean.
        session.EffectiveCleanupStatus.ShouldBe(CleanupStatus.Clean);
        session.RawRevisions.Count.ShouldBe(3);
        PlainText(session.RawDraft).ShouldBe("winner words");
    }

    [Fact]
    public void a_winning_contender_past_the_cleaned_revision_derives_stale()
    {
        var session = WithBase();
        session.BeginCleanup();
        session.CompleteCleanup(Doc("Polished: base words"), "synopsis"); // cleaned Revision 1

        session.SaveDraft(Doc("earlier contender"), baseRevisionNumber: 1, savedAt: At(10)); // Revision 2
        session.SaveDraft(Doc("later contender"), baseRevisionNumber: 1, savedAt: At(11));   // Revision 3, wins

        session.EffectiveCleanupStatus.ShouldBe(CleanupStatus.Stale); // the winner advanced past the run
        session.RawPlainText.ShouldBe("later contender"); // the plaintext projection follows the winner
    }

    [Fact]
    public void a_conflicting_save_whose_content_already_is_the_head_snapshot_appends_nothing()
    {
        var session = WithBase();
        var headDoc = Doc("same words on both devices");
        session.SaveDraft(headDoc, baseRevisionNumber: 1, savedAt: At(10)); // Revision 2

        session.SaveDraft(headDoc, baseRevisionNumber: 1, savedAt: At(11)); // identical content → already preserved

        session.RawRevisions.Count.ShouldBe(2);
        session.RawDraftRevisionNumber.ShouldBe(2);
        session.RawDraftSavedAt.ShouldBe(At(11)); // the later save still claims the Draft slot
    }

    [Fact]
    public void a_save_without_the_new_fields_behaves_exactly_as_before()
    {
        var session = WithBase();
        session.SaveDraft(Doc("later contender"), baseRevisionNumber: 1, savedAt: At(11)); // Revision 2

        session.SaveDraft(Doc("typed on the web")); // no base, no save time → append + become current

        session.RawRevisions.Count.ShouldBe(3);
        PlainText(session.RawDraft).ShouldBe("typed on the web");
        session.RawDraftRevisionNumber.ShouldBe(3);
    }
}
