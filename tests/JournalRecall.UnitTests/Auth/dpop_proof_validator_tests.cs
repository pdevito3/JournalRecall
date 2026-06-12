using JournalRecall.Api.Auth;

namespace JournalRecall.UnitTests.Auth;

/// <summary>
/// Isolated, domain-style unit tests for the deep <see cref="DPoPProofValidator"/> (ADR-0014): the
/// token-endpoint half of RFC 9449 §4.3 — signature against the embedded JWK, htm/htu binding, the
/// iat freshness window, and the canonical jkt thumbprint — against a controllable clock, no host.
/// (jti replay detection is the shared cache's job, issue 0038.)
/// </summary>
public class dpop_proof_validator_tests
{
    private const string Method = "POST";
    private const string Url = "https://localhost/api/auth/login";
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (DPoPProofValidator Validator, FakeClock Clock) NewValidator()
    {
        var clock = new FakeClock(Start);
        return (new DPoPProofValidator(clock, new InMemoryReplayCache(clock)), clock);
    }

    [Fact]
    public async Task a_valid_proof_yields_the_correct_stable_jkt_thumbprint()
    {
        var (validator, clock) = NewValidator();
        using var key = new TestDPoPKey();

        var first = await validator.ValidateAsync(key.CreateProof(Method, Url, issuedAt: clock.GetUtcNow()), Method, Url);
        first.Succeeded.ShouldBeTrue();
        first.Thumbprint.ShouldBe(key.Thumbprint); // RFC 7638 canonical thumbprint of the embedded JWK

        // A second, different proof from the same key binds to the same thumbprint — jkt is a property
        // of the key, not of any single proof.
        var second = await validator.ValidateAsync(key.CreateProof(Method, Url, issuedAt: clock.GetUtcNow()), Method, Url);
        second.Thumbprint.ShouldBe(first.Thumbprint);
    }

    [Fact]
    public async Task a_tampered_signature_is_rejected()
    {
        var (validator, clock) = NewValidator();
        using var key = new TestDPoPKey();

        var proof = TestDPoPKey.TamperSignature(key.CreateProof(Method, Url, issuedAt: clock.GetUtcNow()));

        var result = await validator.ValidateAsync(proof, Method, Url);
        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldBe(DPoPProofFailure.InvalidSignature);
    }

    [Fact]
    public async Task a_stale_iat_outside_the_freshness_window_is_rejected()
    {
        var (validator, clock) = NewValidator();
        using var key = new TestDPoPKey();

        // Just beyond lifetime + skew: the proof was fresh once, but a captured one must die quickly.
        var stale = clock.GetUtcNow() - DPoPProofValidator.ProofLifetime - DPoPProofValidator.ClockSkew - TimeSpan.FromSeconds(1);

        var result = await validator.ValidateAsync(key.CreateProof(Method, Url, issuedAt: stale), Method, Url);
        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldBe(DPoPProofFailure.StaleProof);
    }

    [Fact]
    public async Task a_future_iat_beyond_clock_skew_is_rejected()
    {
        var (validator, clock) = NewValidator();
        using var key = new TestDPoPKey();

        var future = clock.GetUtcNow() + DPoPProofValidator.ClockSkew + TimeSpan.FromSeconds(1);

        var result = await validator.ValidateAsync(key.CreateProof(Method, Url, issuedAt: future), Method, Url);
        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldBe(DPoPProofFailure.FutureProof);
    }

    [Fact]
    public async Task an_iat_within_clock_skew_is_accepted_either_side_of_now()
    {
        var (validator, clock) = NewValidator();
        using var key = new TestDPoPKey();

        var slightlyPast = clock.GetUtcNow() - TimeSpan.FromSeconds(10);
        var slightlyAhead = clock.GetUtcNow() + TimeSpan.FromSeconds(10);

        (await validator.ValidateAsync(key.CreateProof(Method, Url, issuedAt: slightlyPast), Method, Url)).Succeeded.ShouldBeTrue();
        (await validator.ValidateAsync(key.CreateProof(Method, Url, issuedAt: slightlyAhead), Method, Url)).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task an_htm_that_does_not_match_the_request_method_is_rejected()
    {
        var (validator, clock) = NewValidator();
        using var key = new TestDPoPKey();

        var proof = key.CreateProof("GET", Url, issuedAt: clock.GetUtcNow());

        var result = await validator.ValidateAsync(proof, Method, Url);
        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldBe(DPoPProofFailure.MethodMismatch);
    }

    [Fact]
    public async Task an_htu_that_does_not_match_the_request_url_is_rejected()
    {
        var (validator, clock) = NewValidator();
        using var key = new TestDPoPKey();

        var proof = key.CreateProof(Method, "https://localhost/api/auth/refresh", issuedAt: clock.GetUtcNow());

        var result = await validator.ValidateAsync(proof, Method, Url);
        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldBe(DPoPProofFailure.UrlMismatch);
    }

    [Fact]
    public async Task htu_comparison_ignores_query_but_not_path()
    {
        var (validator, clock) = NewValidator();
        using var key = new TestDPoPKey();

        // RFC 9449: htu is scheme + authority + path; query/fragment are excluded from the comparison.
        var withQuery = key.CreateProof(Method, Url + "?utm=x", issuedAt: clock.GetUtcNow());
        (await validator.ValidateAsync(withQuery, Method, Url)).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task a_malformed_proof_is_rejected()
    {
        var (validator, _) = NewValidator();

        var result = await validator.ValidateAsync("not-a-jwt", Method, Url);
        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldBe(DPoPProofFailure.MalformedProof);
    }

    [Fact]
    public async Task a_proof_without_the_dpop_jwt_type_is_rejected()
    {
        var (validator, clock) = NewValidator();
        using var key = new TestDPoPKey();

        var proof = key.CreateProof(Method, Url, issuedAt: clock.GetUtcNow(), typ: "JWT");

        var result = await validator.ValidateAsync(proof, Method, Url);
        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldBe(DPoPProofFailure.MalformedProof);
    }

    [Fact]
    public async Task a_proof_signed_with_an_unsupported_algorithm_is_rejected()
    {
        var (validator, clock) = NewValidator();
        // A genuinely RS256-signed proof (not a hand-rolled JWT): well-formed and verifiable, but the
        // server constrains DPoP to ES256, so the algorithm itself is the rejection.
        using var key = new TestDPoPKey(TestDPoPKey.SigningAlgorithm.Rs256);

        var result = await validator.ValidateAsync(key.CreateProof(Method, Url, issuedAt: clock.GetUtcNow()), Method, Url);
        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldBe(DPoPProofFailure.UnsupportedAlgorithm);
    }

    [Fact]
    public async Task a_missing_jwk_header_is_rejected()
    {
        var (validator, clock) = NewValidator();
        using var key = new TestDPoPKey();

        var proof = key.CreateProof(Method, Url, issuedAt: clock.GetUtcNow(), includeJwk: false);

        var result = await validator.ValidateAsync(proof, Method, Url);
        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldBe(DPoPProofFailure.InvalidJwk);
    }

    [Fact]
    public async Task a_jwk_carrying_a_private_key_is_rejected()
    {
        var (validator, clock) = NewValidator();
        using var key = new TestDPoPKey();

        // A leaked private key in the header is a broken client; binding to it would be meaningless.
        var proof = key.CreateProof(Method, Url, issuedAt: clock.GetUtcNow(), includePrivateKey: true);

        var result = await validator.ValidateAsync(proof, Method, Url);
        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldBe(DPoPProofFailure.InvalidJwk);
    }

    [Fact]
    public async Task a_proof_without_a_jti_is_rejected()
    {
        var (validator, clock) = NewValidator();
        using var key = new TestDPoPKey();

        var proof = key.CreateProof(Method, Url, issuedAt: clock.GetUtcNow(), omitJti: true);

        var result = await validator.ValidateAsync(proof, Method, Url);
        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldBe(DPoPProofFailure.MalformedProof);
    }

    [Fact]
    public async Task presenting_the_same_jti_twice_within_its_lifetime_is_rejected_as_replay()
    {
        var (validator, clock) = NewValidator();
        using var key = new TestDPoPKey();

        // Two otherwise-valid proofs sharing one jti: a captured request resent verbatim-ish.
        var jti = Guid.NewGuid().ToString();
        var first = key.CreateProof(Method, Url, issuedAt: clock.GetUtcNow(), jti: jti);
        var second = key.CreateProof(Method, Url, issuedAt: clock.GetUtcNow(), jti: jti);

        (await validator.ValidateAsync(first, Method, Url)).Succeeded.ShouldBeTrue();

        var replay = await validator.ValidateAsync(second, Method, Url);
        replay.Succeeded.ShouldBeFalse();
        replay.Failure.ShouldBe(DPoPProofFailure.ReplayedProof);
    }

    [Fact]
    public async Task a_jti_is_accepted_again_after_the_prior_entry_expires()
    {
        var (validator, clock) = NewValidator();
        using var key = new TestDPoPKey();

        var jti = Guid.NewGuid().ToString();
        (await validator.ValidateAsync(key.CreateProof(Method, Url, issuedAt: clock.GetUtcNow(), jti: jti), Method, Url))
            .Succeeded.ShouldBeTrue();

        // Past the replay-entry lifetime (proof lifetime + 2× skew): the cache entry has expired, and a
        // proof with that jti would now be stale anyway — re-presenting the id is no longer a replay.
        clock.Advance(DPoPProofValidator.ProofLifetime + DPoPProofValidator.ClockSkew * 2 + TimeSpan.FromSeconds(1));

        var again = await validator.ValidateAsync(
            key.CreateProof(Method, Url, issuedAt: clock.GetUtcNow(), jti: jti), Method, Url);
        again.Succeeded.ShouldBeTrue();
    }

    /// <summary>A controllable <see cref="TimeProvider"/> for time-travel without sleeping.</summary>
    private sealed class FakeClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    /// <summary>An in-memory <see cref="IDPoPReplayCache"/> honoring the fake clock for expiry.</summary>
    private sealed class InMemoryReplayCache(FakeClock clock) : IDPoPReplayCache
    {
        private readonly Dictionary<string, DateTimeOffset> _seen = [];

        public Task<bool> TryAddAsync(string jti, TimeSpan lifetime, CancellationToken cancellationToken = default)
        {
            if (_seen.TryGetValue(jti, out var expiresAt) && expiresAt > clock.GetUtcNow())
                return Task.FromResult(false);
            _seen[jti] = clock.GetUtcNow() + lifetime;
            return Task.FromResult(true);
        }
    }
}
