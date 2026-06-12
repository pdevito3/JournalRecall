using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Auth;

/// <summary>
/// Closes the DPoP enforcement gaps that lived in code with no HTTP coverage (issue 0044): the
/// "exactly one DPoP header" rule (RFC 9449), and the <c>bearer_downgrade</c> telemetry tag emitted
/// when a cnf-bound token is presented as plain Bearer. (The unsupported-algorithm path is covered as a
/// validator unit test.) A regression in any of these would otherwise pass the whole suite.
/// </summary>
public class dpop_enforcement_tests(WebTestFixture fixture) : DPoPTestBase(fixture)
{
    [Fact]
    public async Task a_login_carrying_two_dpop_headers_is_rejected_as_malformed()
    {
        using var key = new TestDPoPKey();
        var client = BearerClient();
        var creds = NewUser();
        await client.PostAsJsonAsync(ApiRoutes.Auth.Register, creds);

        // RFC 9449: a request must carry exactly one DPoP header. Two — even two individually-valid
        // proofs — is a malformed request, rejected before either is validated.
        var url = "https://localhost" + ApiRoutes.Auth.Login;
        var login = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Auth.Login) { Content = JsonContent.Create(creds) };
        login.Headers.Add(DPoPHeader, key.CreateProof("POST", url));
        login.Headers.Add(DPoPHeader, key.CreateProof("POST", url));

        var response = await client.SendAsync(login);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString()
            .ShouldContain("DPoP error=\"invalid_dpop_proof\", error_description=\"MalformedProof\"");
    }

    [Fact]
    public async Task a_bound_token_presented_as_plain_bearer_surfaces_bearer_downgrade_telemetry()
    {
        using var key = new TestDPoPKey();
        var client = BearerClient();
        var tokens = await DPoPSession(client, key);
        RealAuth.ExportedActivities.Clear();

        // The downgrade attempt: a cnf-bound token sent as plain Bearer instead of DPoP.
        var downgrade = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Me);
        downgrade.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        (await client.SendAsync(downgrade)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var spans = RealAuth.ExportedActivities;
        spans.ShouldContain(a =>
            (a.GetTagItem("auth.dpop.rejected") as bool?) == true
            && (a.GetTagItem("auth.dpop.failure") as string) == "bearer_downgrade");

        // The Privacy line (issue 0040): the operator sees the rejection, never the credential.
        spans.ShouldAllBe(a => a.TagObjects.All(t => t.Value == null || !t.Value.ToString()!.Contains(tokens.AccessToken)));
    }
}
