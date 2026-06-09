using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Domain.Corrections.Dtos;
using JournalRecall.Api.Domain.Corrections.Features;
using JournalRecall.Api.Domain.People.Dtos;
using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Domain.Sessions.Features;
using JournalRecall.Api.Domain.Sessions.Services;
using JournalRecall.Api.Domain.Summaries;
using JournalRecall.Api.Domain.Summaries.Features;
using JournalRecall.Api.Domain.Summaries.Services;
using JournalRecall.Api.Exceptions;
using JournalRecall.SharedTestHelpers.Fakes.Sessions;
using static JournalRecall.SharedTestHelpers.Fakes.Sessions.ContentDoc;

namespace JournalRecall.IntegrationTests.FeatureTests.Sessions;

/// <summary>
/// The server half of the OnDevice Engine (issue 0034, ADR-0013) at the integration layer: a submitted
/// client-run Cleanup result is post-processed identically to a server run (parity over the same Raw +
/// Corrections), hard-replace Corrections are re-applied server-side, an older base Raw Revision derives
/// Stale, People proposals respect the approval setting, the period Summaries go Stale, and an invalid
/// payload is rejected leaving the Session untouched. Driven through RecordCleanupResult, no HTTP.
/// </summary>
public class cleanup_result_tests : TestBase
{
    /// <summary>A device-assembled result payload: the CleanupAgent output shape + base Revision + Engine.</summary>
    private static RecordCleanupResult.Request Result(
        string? cleanedMarkdown, int baseRawRevisionNumber = 1, string synopsis = "A short recap of the session.",
        string[]? topics = null, string[]? people = null, string[]? moods = null, string? engine = "OnDevice") =>
        new(cleanedMarkdown, synopsis, topics ?? [], people ?? [], moods ?? [], baseRawRevisionNumber, engine);

    private static async Task<Session> InsertSessionWithRaw(TestingServiceScope scope, string raw)
    {
        var session = new FakeSessionBuilder().WithUserId(scope.CurrentUserId).WithRawText(raw).Build();
        await scope.InsertAsync(session);
        return session;
    }

    [Fact]
    public async Task a_submitted_result_persists_the_same_state_as_a_server_run_over_the_same_raw_and_corrections()
    {
        using var scope = new TestingServiceScope();
        const string raw = "met Sam about the prophecy rollout at work";
        await scope.SendAsync(new CreateCorrection.Command(new CorrectionForWrite("Profisee", ["prophecy"], true)));
        var serverRun = await InsertSessionWithRaw(scope, raw);
        var onDevice = await InsertSessionWithRaw(scope, raw);

        // Server Engine: the scripted model echoes Raw as "Polished: {raw}" plus the suggestion switches.
        CleanupChat.SuggestTopics = ["work"];
        CleanupChat.SuggestPeople = ["Sam"];
        CleanupChat.SuggestMood = "Joyful";
        await scope.GetService<SessionCleanupRunner>().RunAsync(serverRun.Id);

        // OnDevice Engine: the device's model produced the same agent-output shape over the same Raw —
        // including the un-replaced "prophecy" (hard-replace happens server-side on both Engines).
        (await scope.SendAsync(new RecordCleanupResult.Command(onDevice.Id, Result(
            $"Polished: {raw}", topics: ["work"], people: ["Sam"], moods: ["Joyful"])))).ShouldBeTrue();

        var server = await scope.SendAsync(new GetSession.Query(serverRun.Id));
        var device = await scope.SendAsync(new GetSession.Query(onDevice.Id));

        // The persisted outcome is indistinguishable (CONTEXT.md "Engine").
        device!.CleanupStatus.ShouldBe(server!.CleanupStatus);
        device.CleanupStatus.ShouldBe(CleanupStatus.Clean);
        device.CleanedDraft.ShouldBe(server.CleanedDraft); // byte-for-byte canonical JSON, hard-replace included
        device.Synopsis.ShouldBe(server.Synopsis);
        device.Suggestions.ShouldBe(server.Suggestions, ignoreOrder: true);
        device.PeopleProposals.Select(p => (p.Label, p.MatchedPersonId, p.IsNew))
            .ShouldBe(server.PeopleProposals.Select(p => (p.Label, p.MatchedPersonId, p.IsNew)));
        PlainText(device.RawDraft).ShouldBe(raw); // Raw is never touched

        // Both runs appended exactly one Cleaned Revision pinned to the same Raw Revision.
        (await scope.SendAsync(new GetCleanedRevisions.Query(onDevice.Id)))!.Count.ShouldBe(1);
        var pinned = await scope.ExecuteDbContextAsync(db => db.Sessions.AsNoTracking()
            .Where(s => s.Id == serverRun.Id || s.Id == onDevice.Id)
            .Select(s => s.LastCleanedRawRevisionNumber).Distinct().ToListAsync());
        pinned.ShouldBe([1]);
    }

    [Fact]
    public async Task hard_replace_corrections_are_applied_server_side_even_when_the_device_missed_them()
    {
        using var scope = new TestingServiceScope();
        await scope.SendAsync(new CreateCorrection.Command(new CorrectionForWrite("Profisee", ["prophecy"], true)));
        var session = await InsertSessionWithRaw(scope, "met the prophecy team about prophecy");

        // The device knew nothing of the Correction — its output still carries the mishearing.
        await scope.SendAsync(new RecordCleanupResult.Command(session.Id,
            Result("met the prophecy team about prophecy")));

        var dto = await scope.SendAsync(new GetSession.Query(session.Id));
        PlainText(dto!.CleanedDraft).ShouldBe("met the Profisee team about Profisee");
        PlainText(dto.RawDraft).ShouldBe("met the prophecy team about prophecy"); // Raw untouched
    }

    [Fact]
    public async Task a_result_against_an_older_raw_revision_is_recorded_and_derives_stale()
    {
        using var scope = new TestingServiceScope();
        var session = await InsertSessionWithRaw(scope, "first draft");
        await scope.SendAsync(new SaveDraft.Command(session.Id, Doc("first draft, now revised"))); // Revision 2

        // The device cleaned Revision 1 while offline; Raw moved on in the meantime (ADR-0013).
        (await scope.SendAsync(new RecordCleanupResult.Command(session.Id,
            Result("Polished: first draft", baseRawRevisionNumber: 1)))).ShouldBeTrue();

        var dto = await scope.SendAsync(new GetSession.Query(session.Id));
        PlainText(dto!.CleanedDraft).ShouldBe("Polished: first draft"); // recorded, not dropped
        dto.Synopsis.ShouldNotBeEmpty();
        dto.CleanupStatus.ShouldBe(CleanupStatus.Stale); // Raw edited since the base → derives Stale
        (await scope.SendAsync(new GetCleanedRevisions.Query(session.Id)))!.Count.ShouldBe(1);
    }

    [Fact]
    public async Task people_proposals_are_parked_for_review_when_approval_is_required()
    {
        using var scope = new TestingServiceScope(); // RequirePeopleTagApproval defaults to true
        var session = await InsertSessionWithRaw(scope, "had a great day with Sam");

        await scope.SendAsync(new RecordCleanupResult.Command(session.Id,
            Result("Had a great day with Sam.", people: ["Sam"])));

        var dto = await scope.SendAsync(new GetSession.Query(session.Id));
        // The proposal is pending — nothing tagged, nothing added to the directory yet (RICH-009).
        dto!.People.ShouldBeEmpty();
        (await Directory(scope)).ShouldBeEmpty();
        var proposal = dto.PeopleProposals.ShouldHaveSingleItem();
        proposal.Label.ShouldBe("Sam");
        proposal.IsNew.ShouldBeTrue();
    }

    [Fact]
    public async Task with_approval_off_a_submitted_result_tags_resolved_people_inline()
    {
        using var scope = new TestingServiceScope();
        await RequireApproval(scope, false);
        var session = await InsertSessionWithRaw(scope, "called Sam");

        await scope.SendAsync(new RecordCleanupResult.Command(session.Id,
            Result("Called Sam.", people: ["Sam"])));

        var dto = await scope.SendAsync(new GetSession.Query(session.Id));
        dto!.PeopleProposals.ShouldBeEmpty();            // nothing to review — applied at record time
        dto.People.ShouldBe(["Sam"]);                    // tagged inline, projected onto the badges
        (await Directory(scope)).Select(p => p.Label).ShouldBe(["Sam"]); // upserted immediately
    }

    [Fact]
    public async Task recording_a_result_marks_the_affected_period_summaries_stale()
    {
        using var scope = new TestingServiceScope();
        var session = await InsertSessionWithRaw(scope, "a quiet morning"); // CreatedAt = Clock → 2026-01-01
        var day = new DateOnly(2026, 1, 1);
        SummaryChat.Narrative = "DAY-V1";
        await scope.GetService<SummaryGenerator>().GenerateAsync(SummaryPeriod.Day, day);
        (await scope.SendAsync(new GetSummary.Query(SummaryPeriod.Day, day))).Status.ShouldBe(SummaryStatus.Ready);

        await scope.SendAsync(new RecordCleanupResult.Command(session.Id, Result("A quiet morning.")));

        // The recorded result rewrote the Cleaned copy the Day Summary reads (issue 0014).
        (await scope.SendAsync(new GetSummary.Query(SummaryPeriod.Day, day))).Status.ShouldBe(SummaryStatus.Stale);
    }

    [Fact]
    public async Task an_invalid_payload_is_rejected_and_leaves_the_session_untouched()
    {
        using var scope = new TestingServiceScope();
        var session = await InsertSessionWithRaw(scope, "untouched words");

        // Missing Cleaned copy, a base of 0, a future base the server has never seen, a blank Engine.
        await Should.ThrowAsync<ValidationException>(scope.SendAsync(
            new RecordCleanupResult.Command(session.Id, Result(cleanedMarkdown: null))));
        await Should.ThrowAsync<ValidationException>(scope.SendAsync(
            new RecordCleanupResult.Command(session.Id, Result("polished", baseRawRevisionNumber: 0))));
        await Should.ThrowAsync<ValidationException>(scope.SendAsync(
            new RecordCleanupResult.Command(session.Id, Result("polished", baseRawRevisionNumber: 2))));
        await Should.ThrowAsync<ValidationException>(scope.SendAsync(
            new RecordCleanupResult.Command(session.Id, Result("polished", engine: " "))));

        var dto = await scope.SendAsync(new GetSession.Query(session.Id));
        dto!.CleanupStatus.ShouldBe(CleanupStatus.NotRun);
        dto.CleanedDraft.ShouldBeEmpty();
        dto.Synopsis.ShouldBeEmpty();
        PlainText(dto.RawDraft).ShouldBe("untouched words");
        (await scope.SendAsync(new GetCleanedRevisions.Query(session.Id)))!.ShouldBeEmpty();
    }

    [Fact]
    public async Task a_result_cannot_be_recorded_against_another_users_session()
    {
        using var alice = new TestingServiceScope();
        var session = await InsertSessionWithRaw(alice, "alice private words");

        using var bob = new TestingServiceScope();
        (await bob.SendAsync(new RecordCleanupResult.Command(session.Id, Result("polished"))))
            .ShouldBeFalse(); // Privacy invariant — surfaces as 404 at the endpoint

        (await alice.SendAsync(new GetSession.Query(session.Id)))!.CleanupStatus.ShouldBe(CleanupStatus.NotRun);
    }

    private static Task RequireApproval(TestingServiceScope scope, bool required) =>
        scope.ExecuteDbContextAsync(async db =>
        {
            var user = await db.Users.FirstAsync(u => u.Id == scope.CurrentUserId);
            user.RequirePeopleTagApproval = required;
            await db.SaveChangesAsync();
        });

    private static Task<List<PersonDto>> Directory(TestingServiceScope scope) =>
        scope.ExecuteDbContextAsync(db => db.People.Select(p => new PersonDto(p.Id, p.Label)).ToListAsync());
}
