using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using JournalRecall.Api.Auth;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Auth;

/// <summary>
/// The refresh-endpoint hardenings of issue 0042: an unauthenticated caller can't burn a proof against a
/// token that was never issued (token lookup precedes proof validation), an invalid proof still never
/// burns a real rotation, and a bound chain never has its cnf-bound access token written to the auth
/// cookies — the body-only invariant for bound sessions now holds at refresh, not just at login.
/// </summary>
public class dpop_refresh_hardening_tests(WebTestFixture fixture) : DPoPTestBase(fixture)
{
    private static string RefreshUrl => "https://localhost" + ApiRoutes.Auth.Refresh;

    private HttpRequestMessage RefreshRequest(string refreshToken, string proof)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Auth.Refresh)
        {
            Content = JsonContent.Create(new { refreshToken }),
        };
        request.Headers.Add(DPoPHeader, proof);
        return request;
    }

    [Fact]
    public async Task a_garbage_token_with_a_valid_proof_is_rejected_without_consuming_the_proofs_jti()
    {
        using var key = new TestDPoPKey();
        var client = BearerClient();
        var tokens = await DPoPSession(client, key);

        // A fully valid proof (its jti is X), presented with a token that was never issued. The token
        // lookup fails first, so the proof is never validated and X is never written to the replay cache.
        var jti = Guid.NewGuid().ToString();
        var garbage = RefreshRequest("a-token-that-was-never-issued", key.CreateProof("POST", RefreshUrl, jti: jti));
        (await client.SendAsync(garbage)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // A real refresh presenting a fresh proof that reuses jti X succeeds — proof of no pre-auth
        // replay-cache write. (With proof-before-lookup, X would already be burned and this would 401.)
        var legit = RefreshRequest(tokens.RefreshToken, key.CreateProof("POST", RefreshUrl, jti: jti));
        (await client.SendAsync(legit)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task a_stale_proof_with_a_valid_bound_token_is_rejected_without_burning_the_rotation()
    {
        using var key = new TestDPoPKey();
        var client = BearerClient();
        var tokens = await DPoPSession(client, key);

        // The token exists, so the proof IS validated — and a stale one is rejected with the retryable
        // StaleProof challenge, never reaching the rotation.
        var staleProof = key.CreateProof("POST", RefreshUrl, issuedAt: DateTimeOffset.UtcNow.AddMinutes(-5));
        var stale = await client.SendAsync(RefreshRequest(tokens.RefreshToken, staleProof));
        stale.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        stale.Headers.WwwAuthenticate.ToString()
            .ShouldContain("DPoP error=\"invalid_dpop_proof\", error_description=\"StaleProof\"");

        // The very same refresh token then rotates with a fresh proof — the stale proof never burned it.
        (await Refresh(client, tokens.RefreshToken, key)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task a_bound_chain_arriving_via_the_refresh_cookie_never_writes_a_bound_access_cookie()
    {
        using var key = new TestDPoPKey();
        var client = BearerClient();
        var tokens = await DPoPSession(client, key);

        // Force the degenerate state issue 0042 guards: a bound refresh token presented in the refresh
        // COOKIE (not the body) with a valid proof.
        var viaCookie = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Auth.Refresh);
        viaCookie.Headers.Add("Cookie", $"{AuthCookie.RefreshName}={tokens.RefreshToken}");
        viaCookie.Headers.Add(DPoPHeader, key.CreateProof("POST", RefreshUrl));
        var rotated = await client.SendAsync(viaCookie);

        // The rotation reports a bound chain, so the response is the body TokenResponse (still cnf-bound),
        // never a cookie session.
        rotated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = (await rotated.ReadJsonAsync<TokenResponse>())!;
        var access = new JsonWebToken(body.AccessToken);
        access.TryGetPayloadValue<JsonElement>("cnf", out var cnf).ShouldBeTrue();
        cnf.GetProperty("jkt").GetString().ShouldBe(key.Thumbprint);

        // No Set-Cookie writes a bound access token: any access-cookie header is a deletion (empty value),
        // never a minted JWT — so there's no cnf-bound cookie to wedge a follow-up cookie-fallback request.
        var accessCookies = rotated.Headers.TryGetValues("Set-Cookie", out var setCookies)
            ? setCookies.Where(c => c.StartsWith(AuthCookie.AccessName + "=")).ToList()
            : [];
        accessCookies.ShouldAllBe(c => c.StartsWith(AuthCookie.AccessName + "=;"));

        // And the hazard the body-only rule avoids: that bound token, had it ridden the access cookie,
        // is exactly what the resource server rejects when the cookie fallback presents it as plain Bearer.
        var followUp = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Me);
        followUp.Headers.Add("Cookie", $"{AuthCookie.AccessName}={body.AccessToken}");
        (await client.SendAsync(followUp)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
