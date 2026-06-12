using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Auth;

/// <summary>
/// Pins the DPoP error contract the mobile reference flow documents (issue 0040 /
/// docs/mobile/dpop-proof-contract.md): every rejection a client must distinguish, asserted against the
/// actual status codes and <c>WWW-Authenticate</c> challenges the server emits — if these change, the
/// doc is wrong. Also pins the telemetry: rejections surface as span tags with no proof/token material.
/// </summary>
public class dpop_error_contract_tests(WebTestFixture fixture) : DPoPTestBase(fixture)
{
    private static string Challenges(HttpResponseMessage response) =>
        string.Join(" ", response.Headers.WwwAuthenticate.Select(h => h.ToString()));

    [Fact]
    public async Task token_endpoint_rejections_name_the_typed_failure_a_client_retries_or_resigns_on()
    {
        using var key = new TestDPoPKey();
        var client = BearerClient();
        var creds = NewUser();
        await client.PostAsJsonAsync(ApiRoutes.Auth.Register, creds);

        // Stale proof → regenerate and retry: the challenge names the retryable failure exactly.
        var login = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Auth.Login) { Content = JsonContent.Create(creds) };
        login.Headers.Add(DPoPHeader, key.CreateProof("POST", "https://localhost" + ApiRoutes.Auth.Login,
            issuedAt: DateTimeOffset.UtcNow.AddMinutes(-5)));

        var stale = await client.SendAsync(login);
        stale.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Challenges(stale).ShouldContain("DPoP error=\"invalid_dpop_proof\", error_description=\"StaleProof\"");
    }

    [Fact]
    public async Task resource_rejections_carry_the_documented_dpop_challenges()
    {
        using var key = new TestDPoPKey();
        using var thiefKey = new TestDPoPKey();
        var client = BearerClient();
        var tokens = await DPoPSession(client, key);

        // Bound token, no proof at all → invalid_request.
        var noProof = await client.SendAsync(BoundRequest(HttpMethod.Get, ApiRoutes.Me, tokens.AccessToken, proof: null));
        noProof.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Challenges(noProof).ShouldContain("DPoP error=\"invalid_request\"");

        // Proof from a key that doesn't match the token's cnf.jkt → key mismatch.
        var wrongKey = await client.SendAsync(BoundRequest(HttpMethod.Get, ApiRoutes.Me, tokens.AccessToken, thiefKey));
        wrongKey.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Challenges(wrongKey).ShouldContain("DPoP error=\"invalid_dpop_proof\", error_description=\"Invalid 'cnf' value.\"");

        // Stale proof → freshness failure, regenerate and retry.
        var staleProof = key.CreateProof("GET", "https://localhost" + ApiRoutes.Me,
            accessToken: tokens.AccessToken, issuedAt: DateTimeOffset.UtcNow.AddMinutes(-5));
        var stale = await client.SendAsync(BoundRequest(HttpMethod.Get, ApiRoutes.Me, tokens.AccessToken, staleProof));
        stale.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Challenges(stale).ShouldContain("DPoP error=\"invalid_dpop_proof\", error_description=\"Invalid 'iat' value.\"");

        // Replayed proof.
        var proof = key.CreateProof("GET", "https://localhost" + ApiRoutes.Me, accessToken: tokens.AccessToken);
        await client.SendAsync(BoundRequest(HttpMethod.Get, ApiRoutes.Me, tokens.AccessToken, proof));
        var replay = await client.SendAsync(BoundRequest(HttpMethod.Get, ApiRoutes.Me, tokens.AccessToken, proof));
        replay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Challenges(replay).ShouldContain("DPoP error=\"invalid_dpop_proof\", error_description=\"Detected DPoP proof token replay.\"");

        // Proof without the access-token hash (ath is required on resource proofs).
        var noAth = key.CreateProof("GET", "https://localhost" + ApiRoutes.Me); // accessToken omitted
        var missingAth = await client.SendAsync(BoundRequest(HttpMethod.Get, ApiRoutes.Me, tokens.AccessToken, noAth));
        missingAth.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Challenges(missingAth).ShouldContain("DPoP error=\"invalid_dpop_proof\", error_description=\"Invalid 'ath' value.\"");

        // Bound token downgraded to plain Bearer.
        var downgrade = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Me);
        downgrade.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var downgraded = await client.SendAsync(downgrade);
        downgraded.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Challenges(downgraded).ShouldContain("Must use DPoP when using an access token with a 'cnf' claim");
    }

    [Fact]
    public async Task a_revoked_chain_refresh_is_a_plain_401_meaning_sign_in_again()
    {
        using var key = new TestDPoPKey();
        using var thiefKey = new TestDPoPKey();
        var client = BearerClient();
        var tokens = await DPoPSession(client, key);

        // Kill the chain (wrong-key attempt = suspected theft), then refresh legitimately: the revoked
        // chain answers a bare 401 with no DPoP challenge — the documented "fall back to sign-in" signal.
        await Refresh(client, tokens.RefreshToken, thiefKey);
        var revoked = await Refresh(client, tokens.RefreshToken, key);

        revoked.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        revoked.Headers.WwwAuthenticate.ShouldBeEmpty();
    }

    [Fact]
    public async Task dpop_rejections_surface_in_auth_telemetry_without_proof_or_token_material()
    {
        using var key = new TestDPoPKey();
        using var thiefKey = new TestDPoPKey();
        var client = BearerClient();
        var tokens = await DPoPSession(client, key);
        RealAuth.ExportedActivities.Clear();

        // A token-endpoint rejection (stale login proof) …
        var creds = NewUser();
        await client.PostAsJsonAsync(ApiRoutes.Auth.Register, creds);
        var login = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Auth.Login) { Content = JsonContent.Create(creds) };
        var staleProof = key.CreateProof("POST", "https://localhost" + ApiRoutes.Auth.Login,
            issuedAt: DateTimeOffset.UtcNow.AddMinutes(-5));
        login.Headers.Add(DPoPHeader, staleProof);
        await client.SendAsync(login);

        // … and a resource rejection (wrong key).
        var wrongProof = thiefKey.CreateProof("GET", "https://localhost" + ApiRoutes.Me, accessToken: tokens.AccessToken);
        await client.SendAsync(BoundRequest(HttpMethod.Get, ApiRoutes.Me, tokens.AccessToken, wrongProof));

        var spans = RealAuth.ExportedActivities;
        spans.ShouldContain(a => (a.GetTagItem("auth.dpop.failure") as string) == "StaleProof");
        spans.ShouldContain(a => (a.GetTagItem("auth.dpop.failure") as string) == "invalid_dpop_proof");

        // The Privacy line (issue 0040): the operator sees the rejection, never the credential. No span
        // tag carries the proof JWTs or the access token.
        foreach (var secret in new[] { staleProof, wrongProof, tokens.AccessToken })
            spans.ShouldAllBe(a => a.TagObjects.All(t => t.Value == null || !t.Value.ToString()!.Contains(secret)));
    }
}
