using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.IdentityModel.Tokens;

namespace JournalRecall.Api.Auth;

/// <summary>The token-endpoint half of DPoP proof-replay protection (issue 0038): records each seen
/// proof <c>jti</c> for the proof lifetime so a captured proof cannot be presented twice.</summary>
public interface IDPoPReplayCache
{
    /// <summary>Records the <paramref name="jti"/> if unseen and returns true; returns false when it is
    /// already present (a replay).</summary>
    Task<bool> TryAddAsync(string jti, TimeSpan lifetime, CancellationToken cancellationToken = default);
}

/// <summary>
/// Backed by a keyed <see cref="HybridCache"/> registered under an app-owned key (this half stays
/// deliberately Duende-free — ADR-0014 / issue 0041); the auth registration aliases the Duende
/// resource-server half's key to the same backing, so "one cache, swap once for Redis" holds without
/// importing Duende here. In-memory by default so a single-instance self-hosted deployment needs no
/// Redis; a distributed backing is a configuration swap on that one keyed registration.
/// </summary>
internal sealed class HybridDPoPReplayCache(
    [FromKeyedServices(HybridDPoPReplayCache.CacheServiceKey)] HybridCache cache) : IDPoPReplayCache
{
    /// <summary>App-owned DI key for the proof-replay <see cref="HybridCache"/> backing. The auth
    /// registration points the Duende library's keyed registration at this same backing (issue 0041).</summary>
    public const string CacheServiceKey = "journalrecall-dpop-proof-replay";

    private const string Prefix = "dpop-login-replay-jti-";

    public async Task<bool> TryAddAsync(string jti, TimeSpan lifetime, CancellationToken cancellationToken = default)
    {
        // Hash the (attacker-supplied) jti so the cache key has a bounded, uniform shape.
        var key = Prefix + Base64UrlEncoder.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(jti)));

        // Atomic add via HybridCache stampede protection: concurrent presentations of one jti share a
        // single factory invocation, so exactly one caller's marker is stored. The caller whose marker
        // came back won the race (first add); everyone else sees the stored marker and loses (replay).
        // This closes the probe-then-set race where two concurrent refreshes both saw "unseen" (issue
        // 0041) — the only barrier against a captured refresh replayed inside ADR-0005's grace window.
        var marker = Guid.NewGuid().ToString("N");
        var winner = await cache.GetOrCreateAsync(key,
            _ => ValueTask.FromResult(marker),
            new HybridCacheEntryOptions { Expiration = lifetime },
            cancellationToken: cancellationToken);
        return string.Equals(winner, marker, StringComparison.Ordinal);
    }
}
