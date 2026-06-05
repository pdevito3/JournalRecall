using Microsoft.Extensions.Options;
using Shouldly;
using JournalRecall.Api.Auth;
using JournalRecall.Api.Domain.Identity;

namespace JournalRecall.Api.Tests.Domain;

/// <summary>
/// Isolated, domain-style unit tests for the deep <see cref="RefreshTokenService"/> (ADR-0005): the
/// rotation lifecycle, reuse-detection, the grace window, the never-capped sliding window, and the
/// hashed-at-rest guarantee — all against an in-memory store + a controllable clock, no DB.
/// </summary>
public class RefreshTokenServiceTests
{
    private static readonly Guid User = Guid.CreateVersion7();
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (RefreshTokenService Service, InMemoryRefreshTokenStore Store, FakeClock Clock) NewService(
        TimeSpan? inactivity = null, TimeSpan? grace = null)
    {
        var store = new InMemoryRefreshTokenStore();
        var clock = new FakeClock(Start);
        var options = Options.Create(new RefreshTokenOptions
        {
            InactivityWindow = inactivity ?? TimeSpan.FromDays(60),
            GraceWindow = grace ?? TimeSpan.FromSeconds(30),
        });
        return (new RefreshTokenService(store, options, clock), store, clock);
    }

    [Fact]
    public async Task Issue_then_rotate_produces_a_new_distinct_usable_token()
    {
        var (service, _, _) = NewService();

        var issued = await service.IssueAsync(User, "device");
        var rotated = await service.RotateAsync(issued.Token, "device");

        rotated.Succeeded.ShouldBeTrue();
        rotated.UserId.ShouldBe(User);
        rotated.Token.ShouldNotBe(issued.Token);

        // The freshly rotated token is itself usable — the chain continues.
        (await service.RotateAsync(rotated.Token!, "device")).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Reusing_a_rotated_token_outside_the_grace_window_revokes_the_whole_chain()
    {
        var (service, _, clock) = NewService(grace: TimeSpan.FromSeconds(30));

        var issued = await service.IssueAsync(User, null);
        var rotated = await service.RotateAsync(issued.Token, null); // issued.Token now revoked+replaced

        clock.Advance(TimeSpan.FromMinutes(1)); // past the grace window

        // Presenting the already-rotated token is reuse/theft: it fails …
        (await service.RotateAsync(issued.Token, null)).Succeeded.ShouldBeFalse();
        // … and the whole chain is revoked, so even the legitimate successor is now dead.
        (await service.RotateAsync(rotated.Token!, null)).Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task The_grace_window_permits_a_just_rotated_token_briefly_without_revoking_the_chain()
    {
        var (service, _, clock) = NewService(grace: TimeSpan.FromSeconds(30));

        var issued = await service.IssueAsync(User, null);
        var rotated = await service.RotateAsync(issued.Token, null);

        clock.Advance(TimeSpan.FromSeconds(5)); // still inside the grace window

        // A benign double-fire: re-presenting the just-rotated token yields a fresh token …
        var graceful = await service.RotateAsync(issued.Token, null);
        graceful.Succeeded.ShouldBeTrue();
        graceful.Token.ShouldNotBeNull();

        // … and the chain is NOT revoked — the legitimate successor still works.
        (await service.RotateAsync(rotated.Token!, null)).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task The_sliding_window_extends_on_use_and_never_hard_caps()
    {
        var window = TimeSpan.FromDays(60);
        var (service, store, clock) = NewService(inactivity: window);

        var issued = await service.IssueAsync(User, null);
        issued.ExpiresAt.ShouldBe(Start + window);

        string current = issued.Token;
        // Roll well past any plausible absolute cap (≈ 2 years), staying active by using it each time.
        for (var i = 0; i < 12; i++)
        {
            clock.Advance(TimeSpan.FromDays(59)); // always inside the 60-day window
            var rotated = await service.RotateAsync(current, null);
            rotated.Succeeded.ShouldBeTrue();
            rotated.ExpiresAt!.Value.ShouldBe(clock.GetUtcNow() + window); // expiry slides to now + window
            current = rotated.Token!;
        }

        // No absolute cap fired: the latest token is still active far beyond the original window.
        store.Tokens.ShouldContain(t => t.IsActive(clock.GetUtcNow()));
    }

    [Fact]
    public async Task The_raw_token_is_persisted_only_as_a_hash()
    {
        var (service, store, _) = NewService();

        var issued = await service.IssueAsync(User, null);

        var stored = store.Tokens.ShouldHaveSingleItem();
        stored.TokenHash.ShouldNotBe(issued.Token);      // the raw value is never persisted
        stored.TokenHash.Length.ShouldBe(64);            // SHA-256 as hex
        // Looking up by the raw value finds nothing — only the hash is stored.
        (await store.FindByHashAsync(issued.Token)).ShouldBeNull();
    }

    [Fact]
    public async Task RevokeCurrent_ends_only_that_device_while_RevokeAll_ends_every_session()
    {
        var (service, _, _) = NewService();

        // Two independent devices (two chains) for the same User.
        var deviceA = await service.IssueAsync(User, "A");
        var deviceB = await service.IssueAsync(User, "B");

        await service.RevokeCurrentAsync(deviceA.Token);
        (await service.RotateAsync(deviceA.Token, "A")).Succeeded.ShouldBeFalse(); // A is gone
        var bLive = await service.RotateAsync(deviceB.Token, "B");
        bLive.Succeeded.ShouldBeTrue(); // B survives

        // A global revoke kills every live session: B's current successor and a brand-new device alike.
        var deviceC = await service.IssueAsync(User, "C");
        await service.RevokeAllAsync(User);
        (await service.RotateAsync(bLive.Token!, "B")).Succeeded.ShouldBeFalse();
        (await service.RotateAsync(deviceC.Token, "C")).Succeeded.ShouldBeFalse();
    }

    /// <summary>An in-memory <see cref="IRefreshTokenStore"/> for fast, DB-free service tests.</summary>
    private sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
    {
        private readonly List<RefreshToken> _tokens = [];
        public IReadOnlyList<RefreshToken> Tokens => _tokens;

        public Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default)
        {
            _tokens.Add(token);
            return Task.CompletedTask;
        }

        public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
            Task.FromResult(_tokens.FirstOrDefault(t => t.TokenHash == tokenHash));

        public Task<IReadOnlyList<RefreshToken>> FindByChainAsync(Guid chainId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RefreshToken>>(_tokens.Where(t => t.ChainId == chainId).ToList());

        public Task<IReadOnlyList<RefreshToken>> FindActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RefreshToken>>(_tokens.Where(t => t.UserId == userId && t.RevokedAt is null).ToList());

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>A controllable <see cref="TimeProvider"/> for time-travel without sleeping.</summary>
    private sealed class FakeClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }
}
