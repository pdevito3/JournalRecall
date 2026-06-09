using JournalRecall.Api.Domain.Corrections.Dtos;
using JournalRecall.Api.Domain.Corrections.Features;
using JournalRecall.Api.Domain.People.Dtos;
using JournalRecall.Api.Domain.People.Features;
using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.Api.Domain.Sessions.Features;
using JournalRecall.Api.Domain.Sessions.Metadata;
using JournalRecall.Api.Domain.Sessions.Services;
using JournalRecall.Api.Domain.Sync.Dtos;
using JournalRecall.Api.Domain.Sync.Features;
using JournalRecall.Api.Domain.Users.Features;
using JournalRecall.SharedTestHelpers.Fakes.Sessions;

namespace JournalRecall.IntegrationTests.FeatureTests.Sync;

/// <summary>
/// The delta sync change feed (issue 0033, ADR-0013) at the integration layer: a pull returns
/// everything of the caller's that changed since the cursor — Sessions as full current state,
/// Corrections, People, Settings — plus the next cursor. Every mutation (draft save, Cleanup
/// completion, metadata, Suggestion accept) advances a Session's UpdatedAt watermark; unchanged
/// entities stay out; the cursor is monotonic with no gaps or repeats under sequential writes; no
/// cursor = full bootstrap; everything is tenant-scoped. Driven through the handlers, no HTTP.
/// </summary>
public class sync_changes_tests : TestBase
{
    private static Task<SyncChangesDto?> Pull(TestingServiceScope scope, string? since = null) =>
        scope.SendAsync(new GetChanges.Query(since));

    [Fact]
    public async Task bootstrap_with_no_cursor_returns_the_users_full_state()
    {
        using var scope = new TestingServiceScope();
        var session = new FakeSessionBuilder().WithUserId(scope.CurrentUserId).WithRawText("a first entry").Build();
        await scope.InsertAsync(session);
        await scope.SendAsync(new CreateCorrection.Command(new CorrectionForWrite("Profisee", ["prophecy"], false)));
        await scope.SendAsync(new CreatePerson.Command(new PersonForWrite("Sam")));

        var pull = await Pull(scope);

        pull!.Sessions.Select(s => s.Id).ShouldBe([session.Id]);
        pull.Sessions.Single().RawDraft.ShouldBe(session.RawDraft); // full current state, not a stub
        pull.Corrections.Single().CanonicalTerm.ShouldBe("Profisee");
        pull.People.Single().Label.ShouldBe("Sam");
        pull.Settings.ShouldNotBeNull(); // bootstrap always carries Settings
        pull.Cursor.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task a_draft_save_appears_in_the_next_pull_and_unchanged_entities_do_not()
    {
        using var scope = new TestingServiceScope();
        var edited = new FakeSessionBuilder().WithUserId(scope.CurrentUserId).Build();
        var untouched = new FakeSessionBuilder().WithUserId(scope.CurrentUserId).Build();
        await scope.InsertAsync(edited, untouched);
        await scope.SendAsync(new CreatePerson.Command(new PersonForWrite("Sam")));
        var cursor = (await Pull(scope))!.Cursor;

        Clock.Advance(TimeSpan.FromMinutes(1));
        (await scope.SendAsync(new SaveDraft.Command(edited.Id, ContentDoc.Doc("an offline edit")))).ShouldBeTrue();

        var pull = await Pull(scope, cursor);
        pull!.Sessions.Select(s => s.Id).ShouldBe([edited.Id]); // the untouched Session is excluded
        ContentDoc.PlainText(pull.Sessions.Single().RawDraft).ShouldBe("an offline edit");
        pull.Corrections.ShouldBeEmpty();
        pull.People.ShouldBeEmpty();
        pull.Settings.ShouldBeNull(); // unchanged Settings stay out of a delta pull
    }

    [Fact]
    public async Task cleanup_completion_appears_in_the_next_pull()
    {
        using var scope = new TestingServiceScope();
        var session = new FakeSessionBuilder().WithUserId(scope.CurrentUserId).WithRawText("clean me up").Build();
        await scope.InsertAsync(session);
        var cursor = (await Pull(scope))!.Cursor;

        Clock.Advance(TimeSpan.FromMinutes(1));
        await scope.GetService<SessionCleanupRunner>().RunAsync(session.Id);

        var pull = await Pull(scope, cursor);
        var synced = pull!.Sessions.Single(s => s.Id == session.Id);
        synced.CleanupStatus.ShouldBe(CleanupStatus.Clean);
        synced.CleanedDraft.ShouldNotBeNullOrWhiteSpace();
        synced.Synopsis.ShouldBe("A short recap of the session.");
    }

    [Fact]
    public async Task a_metadata_update_appears_in_the_next_pull()
    {
        using var scope = new TestingServiceScope();
        var session = new FakeSessionBuilder().WithUserId(scope.CurrentUserId).Build();
        await scope.InsertAsync(session);
        var cursor = (await Pull(scope))!.Cursor;

        // A Topics-only write mutates nothing but owned children — exactly the case the change feed's
        // root-touch must still surface (issue 0033).
        Clock.Advance(TimeSpan.FromMinutes(1));
        (await scope.SendAsync(new UpdateMetadata.Command(session.Id, new MetadataForWrite(["work"], [], "None"))))
            .ShouldBe(UpdateMetadata.Result.Ok);

        var pull = await Pull(scope, cursor);
        pull!.Sessions.Single(s => s.Id == session.Id).Topics.ShouldBe(["work"]);
    }

    [Fact]
    public async Task a_suggestion_accept_appears_in_the_next_pull()
    {
        using var scope = new TestingServiceScope();
        var session = new FakeSessionBuilder().WithUserId(scope.CurrentUserId).WithRawText("work stuff").Build();
        await scope.InsertAsync(session);
        CleanupChat.SuggestTopics = ["work"];
        await scope.GetService<SessionCleanupRunner>().RunAsync(session.Id);
        var cursor = (await Pull(scope))!.Cursor;

        Clock.Advance(TimeSpan.FromMinutes(1));
        (await scope.SendAsync(new RespondToSuggestion.Command(session.Id, SuggestionKind.Topic, "work", Accept: true)))
            .ShouldBeTrue();

        var pull = await Pull(scope, cursor);
        var synced = pull!.Sessions.Single(s => s.Id == session.Id);
        synced.Topics.ShouldBe(["work"]); // promoted…
        synced.Suggestions.ShouldBeEmpty(); // …and no longer pending
    }

    [Fact]
    public async Task corrections_people_and_settings_changes_appear_in_the_feed()
    {
        using var scope = new TestingServiceScope();
        var cursor = (await Pull(scope))!.Cursor;

        Clock.Advance(TimeSpan.FromMinutes(1));
        await scope.SendAsync(new CreateCorrection.Command(new CorrectionForWrite("Profisee", ["prophecy"], true)));
        Clock.Advance(TimeSpan.FromMinutes(1));
        await scope.SendAsync(new CreatePerson.Command(new PersonForWrite("Sam")));
        Clock.Advance(TimeSpan.FromMinutes(1));
        (await scope.SendAsync(new UpdateUserSettings.Command("America/New_York", true, false)))
            .ShouldBe(UpdateUserSettings.Result.Ok);

        var pull = await Pull(scope, cursor);
        pull!.Sessions.ShouldBeEmpty();
        pull.Corrections.Single().CanonicalTerm.ShouldBe("Profisee");
        pull.People.Single().Label.ShouldBe("Sam");
        pull.Settings.ShouldNotBeNull();
        pull.Settings!.TimeZoneId.ShouldBe("America/New_York");
        pull.Settings.LocationCaptureEnabled.ShouldBeTrue();
        pull.Settings.RequirePeopleTagApproval.ShouldBeFalse();
    }

    [Fact]
    public async Task the_returned_cursor_replayed_yields_only_later_changes()
    {
        using var scope = new TestingServiceScope();
        var session = new FakeSessionBuilder().WithUserId(scope.CurrentUserId).Build();
        await scope.InsertAsync(session);
        var bootstrap = (await Pull(scope))!.Cursor;

        // First write after the bootstrap cursor…
        Clock.Advance(TimeSpan.FromMinutes(1));
        await scope.SendAsync(new SaveDraft.Command(session.Id, ContentDoc.Doc("first edit")));
        var first = await Pull(scope, bootstrap);
        first!.Sessions.Select(s => s.Id).ShouldBe([session.Id]);

        // …its cursor replayed immediately yields nothing (no repeats)…
        var quiet = await Pull(scope, first.Cursor);
        quiet!.Sessions.ShouldBeEmpty();
        quiet.Corrections.ShouldBeEmpty();
        quiet.People.ShouldBeEmpty();
        quiet.Settings.ShouldBeNull();
        quiet.Cursor.ShouldBe(first.Cursor); // monotonic: an empty pull doesn't move the watermark

        // …and only a later write appears on the next replay (no gaps).
        Clock.Advance(TimeSpan.FromMinutes(1));
        await scope.SendAsync(new CreateCorrection.Command(new CorrectionForWrite("Profisee", null, false)));
        var second = await Pull(scope, first.Cursor);
        second!.Sessions.ShouldBeEmpty(); // the earlier edit is not re-sent
        second.Corrections.Single().CanonicalTerm.ShouldBe("Profisee");
    }

    [Fact]
    public async Task another_users_changes_never_appear()
    {
        using var alice = new TestingServiceScope();
        var session = new FakeSessionBuilder().WithUserId(alice.CurrentUserId).Build();
        await alice.InsertAsync(session);
        await alice.SendAsync(new CreateCorrection.Command(new CorrectionForWrite("Profisee", null, false)));
        await alice.SendAsync(new CreatePerson.Command(new PersonForWrite("Sam")));
        (await alice.SendAsync(new UpdateUserSettings.Command("America/New_York", true, false)))
            .ShouldBe(UpdateUserSettings.Result.Ok);

        using var bob = new TestingServiceScope();
        var pull = await Pull(bob);

        pull!.Sessions.ShouldBeEmpty();
        pull.Corrections.ShouldBeEmpty();
        pull.People.ShouldBeEmpty();
        // Bob gets his own (default) Settings, never Alice's.
        pull.Settings.ShouldNotBeNull();
        pull.Settings!.TimeZoneId.ShouldBeNull();
        pull.Settings.LocationCaptureEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task an_unrecognized_cursor_is_rejected()
    {
        using var scope = new TestingServiceScope();

        (await Pull(scope, "not-a-cursor")).ShouldBeNull();
    }
}
