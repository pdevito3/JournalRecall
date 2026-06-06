using JournalRecall.Api.Domain.Sessions;
using JournalRecall.SharedTestHelpers.Fakes.Sessions;

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
        session.CleanedDraft.ShouldBe("Hello world.");
        session.CleanedRevisions.Count.ShouldBe(1);
        session.CleanedHasHandEdits.ShouldBeFalse();
        session.RawDraft.ShouldBe("helo wrld"); // Raw is never touched
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
        session.SaveDraft("new raw");
        session.BeginCleanup();
        session.FailCleanup();

        session.CleanupStatus.ShouldBe(CleanupStatus.Failed);
        session.CleanedDraft.ShouldBe("good copy");   // prior Cleaned copy intact
        session.CleanedRevisions.Count.ShouldBe(1);    // the failed run appended nothing
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
}
