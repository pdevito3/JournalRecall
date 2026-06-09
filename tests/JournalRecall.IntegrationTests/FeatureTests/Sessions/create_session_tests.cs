using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Domain.Sessions.Features;

namespace JournalRecall.IntegrationTests.FeatureTests.Sessions;

/// <summary>
/// Integration reference test (PRD-0003, TEST-0003): CreateSession sent through the real MediatR slice
/// against real SQLite, with no HTTP, persists a Session scoped to the current User. Also covers the
/// client-supplied id contract (ADR-0013, issue 0031): a client GUID is adopted, replaying an owned id
/// is an idempotent no-op, a foreign id is denied without leaking existence, and omitting the id still
/// server-mints the GUID.
/// </summary>
public class create_session_tests : TestBase
{
    [Fact]
    public async Task create_session_persists_and_scopes_to_the_current_user()
    {
        using var scope = new TestingServiceScope();

        var dto = await scope.SendAsync(new CreateSession.Command(null, null));

        dto.ShouldNotBeNull();
        dto.CleanupStatus.ShouldBe(CleanupStatus.NotRun);

        // The persisted row is visible through the tenant-scoped DbContext and owned by this User.
        var persisted = await scope.ExecuteDbContextAsync(db =>
            db.Sessions.FirstOrDefaultAsync(s => s.Id == dto.Id));
        persisted.ShouldNotBeNull();
        persisted!.UserId.ShouldBe(scope.CurrentUserId);
    }

    [Fact]
    public async Task omitting_the_id_still_server_mints_the_guid()
    {
        using var scope = new TestingServiceScope();

        var first = await scope.SendAsync(new CreateSession.Command(null, null));
        var second = await scope.SendAsync(new CreateSession.Command(null, null));

        // No client id → the server mints a fresh GUID per create, exactly as before (issue 0031).
        first!.Id.ShouldNotBe(Guid.Empty);
        second!.Id.ShouldNotBe(first.Id);
    }

    [Fact]
    public async Task a_client_supplied_id_creates_the_session_under_that_guid()
    {
        using var scope = new TestingServiceScope();
        var clientId = Guid.CreateVersion7();

        var dto = await scope.SendAsync(new CreateSession.Command(null, null, clientId));

        dto!.Id.ShouldBe(clientId);
        var persisted = await scope.ExecuteDbContextAsync(db =>
            db.Sessions.FirstOrDefaultAsync(s => s.Id == clientId));
        persisted.ShouldNotBeNull();
        persisted!.UserId.ShouldBe(scope.CurrentUserId);
    }

    [Fact]
    public async Task replaying_a_create_with_an_owned_id_returns_the_existing_session_without_a_duplicate()
    {
        using var scope = new TestingServiceScope();
        var clientId = Guid.CreateVersion7();
        await scope.SendAsync(new CreateSession.Command(null, null, clientId));
        // Work lands between the create and its replay (a dropped response, retried later).
        await scope.SendAsync(new SaveDraft.Command(clientId, "words written before the retry"));

        var replayed = await scope.SendAsync(new CreateSession.Command(null, null, clientId));

        // The existing Session comes back — current state, not a blank re-create — and no second row.
        replayed!.Id.ShouldBe(clientId);
        replayed.RawDraft.ShouldBe("words written before the retry");
        (await scope.ExecuteDbContextAsync(db => db.Sessions.CountAsync())).ShouldBe(1);
    }

    [Fact]
    public async Task an_id_owned_by_another_user_is_rejected_without_leaking_existence()
    {
        using var alice = new TestingServiceScope();
        var clientId = Guid.CreateVersion7();
        await alice.SendAsync(new CreateSession.Command(null, null, clientId));

        using var bob = new TestingServiceScope();
        var rejected = await bob.SendAsync(new CreateSession.Command(null, null, clientId));

        // The same outcome as any not-yours resource (Privacy invariant) — never a duplicate-key error.
        rejected.ShouldBeNull();
        // Alice's Session is untouched and Bob gained nothing.
        var owner = await bob.ExecuteDbContextAsync(db => db.Sessions
            .IgnoreQueryFilters().Where(s => s.Id == clientId).Select(s => s.UserId).SingleAsync());
        owner.ShouldBe(alice.CurrentUserId);
        (await bob.ExecuteDbContextAsync(db => db.Sessions.CountAsync())).ShouldBe(0);
    }
}
