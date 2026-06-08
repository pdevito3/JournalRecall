using JournalRecall.Api.Domain.Sessions;
using JournalRecall.SharedTestHelpers.Fakes.Sessions;
using static JournalRecall.SharedTestHelpers.Fakes.Sessions.ContentDoc;

namespace JournalRecall.UnitTests.Domain.Sessions;

/// <summary>
/// The unit-layer proof of the builder toolkit (PRD-0003, TEST-0002): the Session cleanup state machine
/// exercised purely through the public aggregate API, with arrange reduced to one line via
/// <see cref="FakeSessionBuilder"/>. No host, no DB.
/// </summary>
public class session_cleanup_state_machine_tests
{
    [Fact]
    public void a_new_session_is_empty_and_not_run()
    {
        var session = Session.Create(Guid.CreateVersion7());

        session.RawDraft.ShouldBeEmpty();
        session.LatestRawRevisionNumber.ShouldBe(0);
        session.CleanupStatus.ShouldBe(CleanupStatus.NotRun);
        session.EffectiveCleanupStatus.ShouldBe(CleanupStatus.NotRun);
    }

    [Fact]
    public void a_completed_cleanup_writes_the_cleaned_copy_synopsis_and_a_revision()
    {
        var session = new FakeSessionBuilder().WithRawText("helo wrld").Cleaned("Hello world.").Build();

        session.CleanupStatus.ShouldBe(CleanupStatus.Clean);
        PlainText(session.CleanedDraft).ShouldBe("Hello world."); // content is canonical JSON (ADR-0009)
        session.CleanedRevisions.Count.ShouldBe(1);
        session.CleanedHasHandEdits.ShouldBeFalse();
        PlainText(session.RawDraft).ShouldBe("helo wrld"); // Raw is never touched
    }

    [Fact]
    public void editing_raw_after_a_clean_derives_stale_without_changing_the_stored_status()
    {
        var session = new FakeSessionBuilder().Stale().Build();

        session.EffectiveCleanupStatus.ShouldBe(CleanupStatus.Stale);
        session.CleanupStatus.ShouldBe(CleanupStatus.Clean); // Stale is derived, never persisted
    }

    [Fact]
    public void a_failed_cleanup_records_failure_without_corrupting_a_prior_cleaned_copy()
    {
        // Arrange a Session with a good prior Cleaned copy, then fail a re-run on edited Raw.
        var session = new FakeSessionBuilder().WithRawText("original").Cleaned("good copy").Build();
        session.SaveDraft(Doc("new raw"));
        session.BeginCleanup();
        session.FailCleanup();

        session.CleanupStatus.ShouldBe(CleanupStatus.Failed);
        PlainText(session.CleanedDraft).ShouldBe("good copy");   // prior Cleaned copy intact
        session.CleanedRevisions.Count.ShouldBe(1);    // the failed run appended nothing
    }

    [Fact]
    public void a_failed_re_run_on_edited_raw_after_a_clean_still_derives_stale()
    {
        // Clean, then edit Raw, fail a re-run, then edit Raw again: the prior success means the latest
        // Raw is newer than the last cleaned Revision, so the user is offered a re-run (Stale), not Failed.
        var session = new FakeSessionBuilder().WithRawText("original").Cleaned("good copy").Build();
        session.SaveDraft(Doc("edited once"));
        session.BeginCleanup();
        session.FailCleanup();
        session.SaveDraft(Doc("edited twice"));

        session.CleanupStatus.ShouldBe(CleanupStatus.Failed);              // stored status is untouched
        session.EffectiveCleanupStatus.ShouldBe(CleanupStatus.Stale);     // but the user sees Stale
    }

    [Fact]
    public void a_failed_cleanup_with_no_prior_success_reads_failed()
    {
        // First-ever Cleanup fails: there's no prior successful Cleanup to be stale against.
        var session = new FakeSessionBuilder().WithRawText("never cleaned").Failed().Build();

        session.LastCleanedRawRevisionNumber.ShouldBe(0);
        session.EffectiveCleanupStatus.ShouldBe(CleanupStatus.Failed);
    }

    [Fact]
    public void a_running_cleanup_is_never_overridden_to_stale()
    {
        // Clean, edit Raw, then start a re-run: while it's in flight the status stays Running, even
        // though Raw has advanced past the last cleaned Revision.
        var session = new FakeSessionBuilder().WithRawText("original").Cleaned("good copy").Build();
        session.SaveDraft(Doc("edited"));
        session.BeginCleanup();

        session.LatestRawRevisionNumber.ShouldBeGreaterThan(session.LastCleanedRawRevisionNumber);
        session.EffectiveCleanupStatus.ShouldBe(CleanupStatus.Running);
    }

    [Fact]
    public void a_re_run_supersedes_hand_edits_and_clears_the_flag()
    {
        var session = new FakeSessionBuilder().Cleaned("v1").WithHandEdit("hand edited").Build();
        session.CleanedHasHandEdits.ShouldBeTrue();

        session.BeginCleanup();
        session.CompleteCleanup("v2 fresh", "syn");

        session.CleanedHasHandEdits.ShouldBeFalse();
        session.CleanedDraft.ShouldBe("v2 fresh");
        session.CleanedRevisions.Count.ShouldBe(3); // v1, hand-edit, v2
    }

    [Fact]
    public void the_regeneration_token_advances_on_a_cleanup_but_not_on_a_hand_edit()
    {
        // The client keys the Cleaned editor remount on this token (issue 0028): it must move on a server
        // regeneration (Cleanup run) and stay put on the user's own hand-edit, so a hand-edit autosave
        // doesn't remount the editor mid-keystroke.
        var session = new FakeSessionBuilder().WithRawText("helo").Cleaned("v1").Build();
        var afterCleanup = session.CleanedRegenerationRevisionNumber;
        afterCleanup.ShouldBe(1); // the first Cleaned Revision came from Cleanup

        session.EditCleaned(ContentDoc.Doc("hand edited"));
        session.CleanedRevisions.Count.ShouldBe(2);               // the hand-edit appended a Revision
        session.CleanedRegenerationRevisionNumber.ShouldBe(afterCleanup); // but the token did not move

        session.BeginCleanup();
        session.CompleteCleanup(ContentDoc.Doc("v2 fresh"), "syn");
        session.CleanedRegenerationRevisionNumber.ShouldBe(3);    // a re-run moves it to the new Revision
    }

    [Fact]
    public void an_approved_people_tag_insertion_advances_the_regeneration_token()
    {
        // Approving a People-tag proposal inserts mentions server-side — a regeneration, so the token moves
        // and the client re-seeds the editor with the tagged prose (issue 0028).
        var session = new FakeSessionBuilder().WithRawText("met sam").Cleaned("I met Sam.").Build();
        var beforeApproval = session.CleanedRegenerationRevisionNumber;

        session.ApplyCleanedMentions(ContentDoc.Doc("I met @Sam."));

        session.CleanedRegenerationRevisionNumber.ShouldBeGreaterThan(beforeApproval);
        session.CleanedRegenerationRevisionNumber.ShouldBe(session.CleanedRevisions.Count);
    }
}
