using JournalRecall.Api.Domain.Sessions.Features;
using JournalRecall.Api.Domain.Sessions.Services;
using JournalRecall.SharedTestHelpers.Fakes.Sessions;

namespace JournalRecall.IntegrationTests.FeatureTests.Sessions;

/// <summary>
/// Edit Cleaned + safe re-run + history (issue 0010) at the integration layer: a hand-edit is saved as a
/// Cleaned Revision and flags hand-edits without touching Raw, an unchanged save mints nothing, and a
/// re-run overwrites but retains the prior hand-edited Revision and clears the flag.
/// </summary>
public class cleaned_edit_tests : TestBase
{
    private async Task<Guid> CleanedSession(TestingServiceScope scope, string raw = "raw words")
    {
        var session = new FakeSessionBuilder().WithUserId(scope.CurrentUserId).WithRawText(raw).Build();
        await scope.InsertAsync(session);
        await scope.GetService<SessionCleanupRunner>().RunAsync(session.Id); // Cleaned v1
        return session.Id;
    }

    [Fact]
    public async Task editing_the_cleaned_copy_appends_a_revision_and_flags_hand_edits_without_touching_raw()
    {
        using var scope = new TestingServiceScope();
        var id = await CleanedSession(scope);

        (await scope.SendAsync(new SaveCleaned.Command(id, "my polished version"))).ShouldBeTrue();

        var after = await scope.SendAsync(new GetSession.Query(id));
        after!.CleanedDraft.ShouldBe("my polished version");
        after.CleanedHasHandEdits.ShouldBeTrue();
        after.RawDraft.ShouldBe("raw words"); // Raw untouched

        var cleaned = await scope.SendAsync(new GetCleanedRevisions.Query(id));
        cleaned!.Count.ShouldBe(2);
        (await scope.SendAsync(new GetCleanedRevision.Query(id, 2)))!.Content.ShouldBe("my polished version");

        // Raw history is untouched by the Cleaned edit.
        (await scope.SendAsync(new GetRawRevisions.Query(id)))!.Count.ShouldBe(1);
    }

    [Fact]
    public async Task an_unchanged_cleaned_save_does_not_append_a_revision()
    {
        using var scope = new TestingServiceScope();
        var id = await CleanedSession(scope);

        await scope.SendAsync(new SaveCleaned.Command(id, "Polished: raw words")); // identical → no new Revision

        (await scope.SendAsync(new GetCleanedRevisions.Query(id)))!.Count.ShouldBe(1);
        (await scope.SendAsync(new GetSession.Query(id)))!.CleanedHasHandEdits.ShouldBeFalse();
    }

    [Fact]
    public async Task re_running_after_a_hand_edit_overwrites_but_retains_the_prior_revision_and_clears_the_flag()
    {
        using var scope = new TestingServiceScope();
        var id = await CleanedSession(scope);                                 // Cleaned v1
        await scope.SendAsync(new SaveCleaned.Command(id, "my hand edit"));   // Cleaned v2 (hand-edit)
        (await scope.SendAsync(new GetSession.Query(id)))!.CleanedHasHandEdits.ShouldBeTrue();

        var rerun = await scope.GetService<SessionCleanupRunner>().RunAsync(id); // Cleaned v3
        rerun!.CleanedDraft.ShouldBe("Polished: raw words");
        rerun.CleanedHasHandEdits.ShouldBeFalse();

        // The prior hand-edited Revision is still retrievable from history.
        (await scope.SendAsync(new GetCleanedRevisions.Query(id)))!.Count.ShouldBe(3);
        (await scope.SendAsync(new GetCleanedRevision.Query(id, 2)))!.Content.ShouldBe("my hand edit");
        rerun.RawDraft.ShouldBe("raw words"); // Raw unaffected by the re-run
    }

    [Fact]
    public async Task a_fresh_cleanup_with_no_hand_edits_leaves_the_flag_false()
    {
        using var scope = new TestingServiceScope();
        var session = new FakeSessionBuilder().WithUserId(scope.CurrentUserId).WithRawText("raw words").Build();
        await scope.InsertAsync(session);
        var runner = scope.GetService<SessionCleanupRunner>();

        (await runner.RunAsync(session.Id))!.CleanedHasHandEdits.ShouldBeFalse();
        (await runner.RunAsync(session.Id))!.CleanedHasHandEdits.ShouldBeFalse();
    }
}
