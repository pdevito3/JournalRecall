using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using JournalRecall.Api.Auth;

namespace JournalRecall.UnitTests.Auth;

/// <summary>
/// Covers the <b>real</b> <see cref="HybridDPoPReplayCache"/> against an actual in-memory HybridCache
/// (issue 0041) — the in-memory fake in <see cref="dpop_proof_validator_tests"/> only proves the
/// validator's use of the seam. The load-bearing property is atomicity: concurrent presentations of one
/// jti must yield exactly one winner, because the jti check is the only barrier against a captured
/// refresh replayed inside ADR-0005's grace window.
/// </summary>
public class hybrid_dpop_replay_cache_tests
{
    private static HybridDPoPReplayCache NewCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        var cache = services.BuildServiceProvider().GetRequiredService<HybridCache>();
        return new HybridDPoPReplayCache(cache);
    }

    [Fact]
    public async Task concurrent_adds_of_one_jti_produce_exactly_one_winner()
    {
        var cache = NewCache();
        var jti = Guid.NewGuid().ToString();

        // A realistic burst: the captured-refresh-replayed-concurrently scenario the atomic add exists for.
        var results = await Task.WhenAll(
            Enumerable.Range(0, 64).Select(_ => cache.TryAddAsync(jti, TimeSpan.FromMinutes(1))));

        results.Count(won => won).ShouldBe(1);
    }

    [Fact]
    public async Task a_second_sequential_add_within_the_lifetime_loses()
    {
        var cache = NewCache();
        var jti = Guid.NewGuid().ToString();

        (await cache.TryAddAsync(jti, TimeSpan.FromMinutes(1))).ShouldBeTrue();
        (await cache.TryAddAsync(jti, TimeSpan.FromMinutes(1))).ShouldBeFalse();
    }

    [Fact]
    public async Task an_add_after_the_entry_expires_wins_again()
    {
        var cache = NewCache();
        var jti = Guid.NewGuid().ToString();
        var lifetime = TimeSpan.FromSeconds(1);

        (await cache.TryAddAsync(jti, lifetime)).ShouldBeTrue();
        (await cache.TryAddAsync(jti, lifetime)).ShouldBeFalse();

        // Once the entry's lifetime has elapsed the id is presentable again (the proof itself would be
        // stale by then — the cache need not outlive it).
        await Task.Delay(lifetime + TimeSpan.FromMilliseconds(750));

        (await cache.TryAddAsync(jti, lifetime)).ShouldBeTrue();
    }
}
