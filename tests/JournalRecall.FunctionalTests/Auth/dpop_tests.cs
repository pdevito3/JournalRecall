using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Auth;

/// <summary>
/// End-to-end DPoP sender-constraining for the bearer path (ADR-0014 / issues 0037–0039): a login that
/// presents a proof receives a cnf-bound token usable only with a fresh proof from the same key, the
/// refresh chain is bound to the same key, replayed proofs die, and proof-less logins plus the web
/// cookie flow behave exactly as before.
/// </summary>
public class dpop_tests(WebTestFixture fixture) : DPoPTestBase(fixture)
{
    [Fact]
    public async Task login_with_a_valid_proof_mints_a_cnf_bound_token_in_the_body_with_no_cookies()
    {
        using var key = new TestDPoPKey();
        var client = BearerClient();

        var login = await DPoPLogin(client, key);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        // A bound session is a bearer session: tokens in the body, never in the auth cookies.
        login.Headers.Contains("Set-Cookie").ShouldBeFalse();

        var tokens = (await login.ReadJsonAsync<TokenResponse>())!;
        var access = new JsonWebToken(tokens.AccessToken);
        access.TryGetPayloadValue<JsonElement>("cnf", out var cnf).ShouldBeTrue();
        cnf.GetProperty("jkt").GetString().ShouldBe(key.Thumbprint);
    }

    [Fact]
    public async Task a_bound_token_with_a_matching_fresh_proof_reaches_a_protected_endpoint()
    {
        using var key = new TestDPoPKey();
        var client = BearerClient();
        var tokens = await DPoPSession(client, key);

        var me = await client.SendAsync(BoundRequest(HttpMethod.Get, ApiRoutes.Me, tokens.AccessToken, key));

        me.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task a_bound_token_without_a_proof_is_challenged_with_dpop()
    {
        using var key = new TestDPoPKey();
        var client = BearerClient();
        var tokens = await DPoPSession(client, key);

        var me = await client.SendAsync(BoundRequest(HttpMethod.Get, ApiRoutes.Me, tokens.AccessToken, proof: null));

        me.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        me.Headers.WwwAuthenticate.ToString().ShouldContain("DPoP");
    }

    [Fact]
    public async Task a_bound_token_with_a_wrong_key_proof_is_challenged_with_dpop()
    {
        using var key = new TestDPoPKey();
        using var thiefKey = new TestDPoPKey();
        var client = BearerClient();
        var tokens = await DPoPSession(client, key);

        // The stolen-token scenario: the attacker holds the token but not the bound private key.
        var me = await client.SendAsync(BoundRequest(HttpMethod.Get, ApiRoutes.Me, tokens.AccessToken, thiefKey));

        me.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        me.Headers.WwwAuthenticate.ToString().ShouldContain("DPoP");
    }

    [Fact]
    public async Task a_bound_token_presented_as_plain_bearer_is_rejected()
    {
        using var key = new TestDPoPKey();
        var client = BearerClient();
        var tokens = await DPoPSession(client, key);

        // Downgrade attempt: a cnf-bound token must travel as "Authorization: DPoP", never plain Bearer.
        var me = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Me);
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        (await client.SendAsync(me)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task login_with_an_invalid_proof_is_rejected_with_a_dpop_challenge()
    {
        var client = BearerClient();
        var creds = NewUser();
        await client.PostAsJsonAsync(ApiRoutes.Auth.Register, creds);

        var login = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Auth.Login)
        {
            Content = JsonContent.Create(creds),
        };
        login.Headers.Add(DPoPHeader, "not-a-proof");

        var response = await client.SendAsync(login);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().ShouldContain("invalid_dpop_proof");
    }

    [Fact]
    public async Task a_replayed_proof_is_rejected_at_the_login_endpoint()
    {
        using var key = new TestDPoPKey();
        var client = BearerClient();
        var creds = NewUser();
        await client.PostAsJsonAsync(ApiRoutes.Auth.Register, creds);

        // One proof, presented twice: the first login succeeds and burns the jti; the replay is 401.
        var proof = key.CreateProof("POST", "https://localhost" + ApiRoutes.Auth.Login);

        var first = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Auth.Login) { Content = JsonContent.Create(creds) };
        first.Headers.Add(DPoPHeader, proof);
        (await client.SendAsync(first)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var replay = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Auth.Login) { Content = JsonContent.Create(creds) };
        replay.Headers.Add(DPoPHeader, proof);
        var rejected = await client.SendAsync(replay);
        rejected.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        rejected.Headers.WwwAuthenticate.ToString().ShouldContain("invalid_dpop_proof");
    }

    [Fact]
    public async Task a_replayed_proof_is_rejected_at_a_protected_resource_endpoint()
    {
        using var key = new TestDPoPKey();
        var client = BearerClient();
        var tokens = await DPoPSession(client, key);

        // One resource proof, presented twice (the library half's replay detection, issue 0038).
        var proof = key.CreateProof("GET", "https://localhost" + ApiRoutes.Me, accessToken: tokens.AccessToken);

        (await client.SendAsync(BoundRequest(HttpMethod.Get, ApiRoutes.Me, tokens.AccessToken, proof)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var replayed = await client.SendAsync(BoundRequest(HttpMethod.Get, ApiRoutes.Me, tokens.AccessToken, proof));
        replayed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        replayed.Headers.WwwAuthenticate.ToString().ShouldContain("DPoP");
    }

    [Fact]
    public async Task a_bound_chain_refreshes_with_a_proof_from_the_same_key_and_stays_bound()
    {
        using var key = new TestDPoPKey();
        var client = BearerClient();
        var tokens = await DPoPSession(client, key);

        var refresh = await Refresh(client, tokens.RefreshToken, key);
        refresh.StatusCode.ShouldBe(HttpStatusCode.OK);

        // The rotated session is still sender-constrained: new access token carries the same cnf.jkt …
        var rotated = (await refresh.ReadJsonAsync<TokenResponse>())!;
        rotated.RefreshToken.ShouldNotBe(tokens.RefreshToken); // … and the refresh token rotated (ADR-0005)
        var access = new JsonWebToken(rotated.AccessToken);
        access.TryGetPayloadValue<JsonElement>("cnf", out var cnf).ShouldBeTrue();
        cnf.GetProperty("jkt").GetString().ShouldBe(key.Thumbprint);

        // … and is usable end-to-end with a fresh proof.
        (await client.SendAsync(BoundRequest(HttpMethod.Get, ApiRoutes.Me, rotated.AccessToken, key)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task refreshing_a_bound_chain_with_a_different_key_is_rejected_and_revokes_the_chain()
    {
        using var key = new TestDPoPKey();
        using var thiefKey = new TestDPoPKey();
        var client = BearerClient();
        var tokens = await DPoPSession(client, key);

        // The thief holds the refresh token but proves the wrong key: rejected …
        (await Refresh(client, tokens.RefreshToken, thiefKey)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // … and the whole chain is revoked — even the legitimate key can no longer refresh.
        (await Refresh(client, tokens.RefreshToken, key)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task refreshing_a_bound_chain_with_no_proof_is_rejected_and_revokes_the_chain()
    {
        using var key = new TestDPoPKey();
        var client = BearerClient();
        var tokens = await DPoPSession(client, key);

        // A bare stolen refresh token, no key at all: the exact gap ADR-0014 closes.
        (await Refresh(client, tokens.RefreshToken, proofKey: null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Refresh(client, tokens.RefreshToken, key)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task login_without_a_dpop_header_mints_an_unbound_cookie_token_exactly_as_today()
    {
        var client = RealAuth.CreateClient();
        var creds = NewUser();
        await client.PostAsJsonAsync(ApiRoutes.Auth.Register, creds);

        var login = await client.PostAsJsonAsync(ApiRoutes.Auth.Login, creds);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        // The web flow stays deliberately unbound (ADR-0014): cookie token, no cnf claim, /me works
        // with no proof anywhere in sight.
        var cookieToken = new JsonWebToken(CookieValue(login, "__Host-jr_auth"));
        cookieToken.TryGetPayloadValue<JsonElement>("cnf", out _).ShouldBeFalse();

        (await client.GetAsync(ApiRoutes.Me)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
