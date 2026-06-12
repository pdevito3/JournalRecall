using Microsoft.Extensions.Options;
using JournalRecall.Api.Auth;
using JournalRecall.Api.Domain.Identity;

namespace JournalRecall.UnitTests.Auth;

/// <summary>
/// Isolated unit tests for the DPoP-bound refresh chain (ADR-0014 / issue 0039), mirroring
/// <see cref="refresh_token_service_tests"/>: a bound chain rotates only with proof-of-possession of its
/// key, a mismatch revokes the chain like reuse-detection, the binding survives rotation, and the
/// ADR-0005 guarantees (grace window, sliding expiry, reuse-detection) still hold for bound chains.
/// </summary>
public class refresh_token_binding_tests
{
    private static readonly Guid User = Guid.CreateVersion7();
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private const string DeviceKey = "jkt-device-key-thumbprint";
    private const string WrongKey = "jkt-attacker-key-thumbprint";

    private static (RefreshTokenService Service, InMemoryRefreshTokenStore Store, FakeClock Clock) NewService(
        TimeSpan? grace = null)
    {
        var store = new InMemoryRefreshTokenStore();
        var clock = new FakeClock(Start);
        var options = Options.Create(new RefreshTokenOptions
        {
            InactivityWindow = TimeSpan.FromDays(60),
            GraceWindow = grace ?? TimeSpan.FromSeconds(30),
        });
        return (new RefreshTokenService(store, options, clock), store, clock);
    }

    [Fact]
    public async Task a_bound_chain_rotates_with_the_matching_thumbprint_and_the_successor_inherits_the_binding()
    {
        var (service, store, _) = NewService();

        var issued = await service.IssueAsync(User, "phone", DeviceKey);
        var rotated = await service.RotateAsync(issued.Token, "phone", DeviceKey);

        rotated.Succeeded.ShouldBeTrue();
        rotated.BoundKeyThumbprint.ShouldBe(DeviceKey); // the caller mints a bound access token from this

        // The binding is a property of the chain, not of any single token: every minted row carries it.
        store.Tokens.ShouldAllBe(t => t.BoundKeyThumbprint == DeviceKey);

        // And the successor keeps requiring the key.
        (await service.RotateAsync(rotated.Token!, "phone", DeviceKey)).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task a_mismatched_thumbprint_revokes_the_whole_chain_as_suspected_theft()
    {
        var (service, _, _) = NewService();

        var issued = await service.IssueAsync(User, "phone", DeviceKey);

        // A stolen refresh token presented with the thief's key: rejected …
        (await service.RotateAsync(issued.Token, "thief", WrongKey)).Succeeded.ShouldBeFalse();
        // … and the chain is dead — even the legitimate device can no longer rotate.
        (await service.RotateAsync(issued.Token, "phone", DeviceKey)).Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task a_missing_proof_on_a_bound_chain_revokes_the_chain()
    {
        var (service, _, _) = NewService();

        var issued = await service.IssueAsync(User, "phone", DeviceKey);

        // A bare refresh token without any proof is the exact theft scenario binding exists to stop.
        (await service.RotateAsync(issued.Token, "thief", presentedKeyThumbprint: null)).Succeeded.ShouldBeFalse();
        (await service.RotateAsync(issued.Token, "phone", DeviceKey)).Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task an_unbound_chain_rotates_exactly_as_today_with_or_without_a_presented_key()
    {
        var (service, store, _) = NewService();

        var issued = await service.IssueAsync(User, "web");

        // No proof — the web cookie flow.
        var rotated = await service.RotateAsync(issued.Token, "web");
        rotated.Succeeded.ShouldBeTrue();
        rotated.BoundKeyThumbprint.ShouldBeNull();

        // A proof on an unbound chain is ignored: rotation succeeds and the chain stays unbound.
        var withKey = await service.RotateAsync(rotated.Token!, "web", DeviceKey);
        withKey.Succeeded.ShouldBeTrue();
        withKey.BoundKeyThumbprint.ShouldBeNull();
        store.Tokens.ShouldAllBe(t => t.BoundKeyThumbprint == null);
    }

    [Fact]
    public async Task reuse_detection_still_revokes_a_bound_chain_even_with_the_matching_key()
    {
        var (service, _, clock) = NewService(grace: TimeSpan.FromSeconds(30));

        var issued = await service.IssueAsync(User, "phone", DeviceKey);
        var rotated = await service.RotateAsync(issued.Token, "phone", DeviceKey);

        clock.Advance(TimeSpan.FromMinutes(1)); // past the grace window

        // ADR-0005 regression: possession of the key does not excuse reuse of a rotated token.
        (await service.RotateAsync(issued.Token, "phone", DeviceKey)).Succeeded.ShouldBeFalse();
        (await service.RotateAsync(rotated.Token!, "phone", DeviceKey)).Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task the_grace_window_still_tolerates_a_double_fire_on_a_bound_chain()
    {
        var (service, _, clock) = NewService(grace: TimeSpan.FromSeconds(30));

        var issued = await service.IssueAsync(User, "phone", DeviceKey);
        var rotated = await service.RotateAsync(issued.Token, "phone", DeviceKey);

        clock.Advance(TimeSpan.FromSeconds(5)); // inside the grace window

        // ADR-0005 regression: a benign double-fire with the right key still gets a fresh bound token.
        var graceful = await service.RotateAsync(issued.Token, "phone", DeviceKey);
        graceful.Succeeded.ShouldBeTrue();
        graceful.BoundKeyThumbprint.ShouldBe(DeviceKey);
        (await service.RotateAsync(rotated.Token!, "phone", DeviceKey)).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task sliding_expiry_still_extends_on_use_for_a_bound_chain()
    {
        var (service, _, clock) = NewService();

        var issued = await service.IssueAsync(User, "phone", DeviceKey);

        clock.Advance(TimeSpan.FromDays(59)); // still inside the 60-day window
        var rotated = await service.RotateAsync(issued.Token, "phone", DeviceKey);

        rotated.Succeeded.ShouldBeTrue();
        rotated.ExpiresAt!.Value.ShouldBe(clock.GetUtcNow() + TimeSpan.FromDays(60)); // expiry slid forward
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
