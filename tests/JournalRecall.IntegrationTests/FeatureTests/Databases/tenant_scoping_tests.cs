using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Domain.Identity;
using JournalRecall.Api.Domain.People;
using JournalRecall.Api.Domain.Sessions;

namespace JournalRecall.IntegrationTests.FeatureTests.Databases;

/// <summary>
/// The Privacy invariant as enforced by the marker-driven tenant filter (issue 0030, ADR-0012): an
/// <c>ITenantScoped</c> entity created by one User is invisible to another through the automatic filter,
/// not by accident — <c>IgnoreQueryFilters</c> proves the row exists and only the filter hides it. The
/// regression guard pins ADR-0005: <c>RefreshToken</c> carries a UserId but is deliberately <b>not</b>
/// scoped, so a token stays readable with no current user established (rotation must work post-expiry).
/// </summary>
public class tenant_scoping_tests : TestBase
{
    [Fact]
    public async Task a_tenant_scoped_entity_created_by_one_user_is_invisible_to_another()
    {
        using var alice = new TestingServiceScope();
        var aliceSession = Session.Create(alice.CurrentUserId);
        var alicePerson = Person.Create(alice.CurrentUserId, "Confidant");
        await alice.InsertAsync(aliceSession);
        await alice.InsertAsync(alicePerson);

        using var bob = new TestingServiceScope();

        // Through the automatic ITenantScoped filter, Bob sees none of Alice's rows.
        (await bob.ExecuteDbContextAsync(db => db.Sessions.AnyAsync(s => s.Id == aliceSession.Id)))
            .ShouldBeFalse();
        (await bob.ExecuteDbContextAsync(db => db.People.AnyAsync(p => p.Id == alicePerson.Id)))
            .ShouldBeFalse();

        // The rows do exist — only the tenant filter hides them (proven by ignoring it).
        (await bob.ExecuteDbContextAsync(db => db.Sessions.IgnoreQueryFilters().AnyAsync(s => s.Id == aliceSession.Id)))
            .ShouldBeTrue();
    }

    [Fact]
    public async Task refresh_tokens_are_not_tenant_filtered()
    {
        using var alice = new TestingServiceScope();
        var token = RefreshToken.Issue(
            alice.CurrentUserId, tokenHash: "hash-0030", chainId: Guid.CreateVersion7(),
            createdAt: Clock.GetUtcNow(), expiresAt: Clock.GetUtcNow().AddDays(30), deviceLabel: null);
        await alice.InsertAsync(token);

        // Refresh runs with no current user established, so a different scope must still read the token —
        // a tenant filter here would hide the very row rotation needs (ADR-0005).
        using var bob = new TestingServiceScope();
        (await bob.ExecuteDbContextAsync(db => db.RefreshTokens.AnyAsync(t => t.Id == token.Id)))
            .ShouldBeTrue();
    }
}
