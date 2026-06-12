using System.Net;
using System.Net.Http.Json;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Auth;

/// <summary>
/// The guarantee sweep (issue 0040): the standing auth invariants hold when the session is DPoP-bound —
/// Admin-disable and password-change revoke the bound chain, logout revokes only the chain it targets,
/// and the Privacy invariant (tenant isolation) holds for requests authenticated via bound tokens.
/// </summary>
public class dpop_guarantee_tests(WebTestFixture fixture) : DPoPTestBase(fixture)
{
    private sealed record AdminUserDto(Guid Id, string Username, List<string> Roles, bool IsDisabled);
    private sealed record SessionDto(Guid Id);

    [Fact]
    public async Task admin_disable_revokes_a_dpop_bound_session()
    {
        using var key = new TestDPoPKey();
        var client = BearerClient();
        var creds = NewUser();
        var tokens = await DPoPSession(client, key, creds);

        var admin = await AdminClient();
        var target = (await admin.GetFromJsonAsync<List<AdminUserDto>>(ApiRoutes.Admin.Users, HttpClientExtensions.Web))!
            .Single(u => u.Username == creds.Username);
        (await admin.PostAsync(ApiRoutes.Admin.Disable(target.Id), null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // The device key doesn't outlive the kill switch: even a valid proof can't refresh a disabled
        // user's bound chain.
        (await Refresh(client, tokens.RefreshToken, key)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task password_change_revokes_the_bound_chain()
    {
        using var key = new TestDPoPKey();
        var creds = NewUser();

        // The same user holds a web cookie session and a DPoP-bound bearer session.
        var web = RealAuth.CreateClient();
        await web.PostAsJsonAsync(ApiRoutes.Auth.Register, creds);
        await web.PostAsJsonAsync(ApiRoutes.Auth.Login, creds);

        var bearer = BearerClient();
        var login = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Auth.Login) { Content = JsonContent.Create(creds) };
        login.Headers.Add(DPoPHeader, key.CreateProof("POST", "https://localhost" + ApiRoutes.Auth.Login));
        var tokens = (await (await bearer.SendAsync(login)).ReadJsonAsync<TokenResponse>())!;

        // Changing the password from the web revokes every other session (ADR-0005) — including the
        // bound one: the device key does not exempt a chain from the kill switch.
        var change = await web.PostAsJsonAsync(ApiRoutes.Auth.ChangePassword,
            new { currentPassword = Password, newPassword = "an entirely new passphrase" });
        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await Refresh(bearer, tokens.RefreshToken, key)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task logout_revokes_only_the_bound_chain_it_targets()
    {
        using var keyA = new TestDPoPKey();
        using var keyB = new TestDPoPKey();
        var creds = NewUser();
        var client = BearerClient();

        // Two bound devices for the same user: two keys, two chains.
        var tokensA = await DPoPSession(client, keyA, creds);

        var loginB = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Auth.Login) { Content = JsonContent.Create(creds) };
        loginB.Headers.Add(DPoPHeader, keyB.CreateProof("POST", "https://localhost" + ApiRoutes.Auth.Login));
        var tokensB = (await (await client.SendAsync(loginB)).ReadJsonAsync<TokenResponse>())!;

        // Device A logs out — authenticated as a bound bearer request (token + fresh proof).
        var logout = BoundRequest(HttpMethod.Post, ApiRoutes.Auth.Logout, tokensA.AccessToken, keyA);
        (await client.SendAsync(logout)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // A's chain is dead; B's bound session is untouched (logout-this-device, ADR-0005).
        (await Refresh(client, tokensA.RefreshToken, keyA)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Refresh(client, tokensB.RefreshToken, keyB)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task tenant_isolation_holds_for_requests_authenticated_via_bound_tokens()
    {
        using var aliceKey = new TestDPoPKey();
        using var bobKey = new TestDPoPKey();
        var client = BearerClient();
        var alice = await DPoPSession(client, aliceKey);
        var bob = await DPoPSession(client, bobKey);

        // Alice journals over her bound session.
        var created = await client.SendAsync(BoundRequest(HttpMethod.Post, ApiRoutes.Sessions.Create(), alice.AccessToken, aliceKey));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var sessionId = (await created.ReadJsonAsync<SessionDto>())!.Id;

        // Privacy invariant: Bob's bound token gets a 404 — never the content, never a 403 that leaks
        // existence — exactly as the cookie flow behaves.
        (await client.SendAsync(BoundRequest(HttpMethod.Get, ApiRoutes.Sessions.Get(sessionId), bob.AccessToken, bobKey)))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // And Alice still reads her own.
        (await client.SendAsync(BoundRequest(HttpMethod.Get, ApiRoutes.Sessions.Get(sessionId), alice.AccessToken, aliceKey)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
