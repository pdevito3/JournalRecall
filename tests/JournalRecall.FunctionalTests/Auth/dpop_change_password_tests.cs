using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using JournalRecall.Api.Databases;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Auth;

/// <summary>
/// Change-password from a DPoP-bound session re-establishes a bound session (issue 0043): the calling
/// device's binding survives the revoke-all, the replacement tokens come back in the body (never
/// cookies), no orphan unbound chain is minted, and the standing revoke-all guarantee still kills every
/// other session. The web cookie flow is unchanged (covered by forced_password_change_tests).
/// </summary>
public class dpop_change_password_tests(WebTestFixture fixture) : DPoPTestBase(fixture)
{
    private const string NewPassword = "an entirely new passphrase";

    private HttpRequestMessage BoundChangePassword(string accessToken, TestDPoPKey key, string currentPassword)
    {
        var request = BoundRequest(HttpMethod.Post, ApiRoutes.Auth.ChangePassword, accessToken, key);
        request.Content = JsonContent.Create(new { currentPassword, newPassword = NewPassword });
        return request;
    }

    [Fact]
    public async Task a_bound_caller_re_establishes_a_bound_session_in_the_body_with_the_same_key()
    {
        using var key = new TestDPoPKey();
        var client = BearerClient();
        var creds = NewUser();
        var tokens = await DPoPSession(client, key, creds);

        var changed = await client.SendAsync(BoundChangePassword(tokens.AccessToken, key, creds.Password));

        // 200 with a body TokenResponse, no cookies — the bound bearer posture, mirroring login.
        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        changed.Headers.Contains("Set-Cookie").ShouldBeFalse();

        var rotated = (await changed.ReadJsonAsync<TokenResponse>())!;
        var access = new JsonWebToken(rotated.AccessToken);
        access.TryGetPayloadValue<JsonElement>("cnf", out var cnf).ShouldBeTrue();
        cnf.GetProperty("jkt").GetString().ShouldBe(key.Thumbprint); // same key — possession just proven

        // The replacement refresh chain rotates with a proof from the same key, end-to-end.
        (await Refresh(client, rotated.RefreshToken, key)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task a_bound_change_password_revokes_every_other_session_and_the_old_bound_chain()
    {
        using var key = new TestDPoPKey();
        using var otherKey = new TestDPoPKey();
        var creds = NewUser();
        var client = BearerClient();

        // The calling bound device, plus the same user holding a web cookie session and a second bound chain.
        var tokens = await DPoPSession(client, key, creds);

        var web = RealAuth.CreateClient();
        (await web.PostAsJsonAsync(ApiRoutes.Auth.Login, creds)).EnsureSuccessStatusCode();

        var loginOther = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Auth.Login) { Content = JsonContent.Create(creds) };
        loginOther.Headers.Add(DPoPHeader, otherKey.CreateProof("POST", "https://localhost" + ApiRoutes.Auth.Login));
        var otherTokens = (await (await client.SendAsync(loginOther)).ReadJsonAsync<TokenResponse>())!;

        var changed = (await (await client.SendAsync(BoundChangePassword(tokens.AccessToken, key, creds.Password)))
            .ReadJsonAsync<TokenResponse>())!;

        // Every other session is dead: the web cookie chain, the other bound chain, and the old chain this
        // device changed from (its refresh token was revoked by the revoke-all) …
        (await web.PostAsync(ApiRoutes.Auth.Refresh, null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Refresh(client, otherTokens.RefreshToken, otherKey)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Refresh(client, tokens.RefreshToken, key)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // … and only the new bound chain survives.
        (await Refresh(client, changed.RefreshToken, key)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task a_bound_change_password_mints_no_orphan_unbound_chain()
    {
        using var key = new TestDPoPKey();
        var client = BearerClient();
        var creds = NewUser();
        var tokens = await DPoPSession(client, key, creds);

        var changed = (await (await client.SendAsync(BoundChangePassword(tokens.AccessToken, key, creds.Password)))
            .ReadJsonAsync<TokenResponse>())!;
        var userId = Guid.Parse(new JsonWebToken(changed.AccessToken).GetPayloadValue<string>(JwtRegisteredClaimNames.Sub));

        // The only live chain is the bound replacement — no unbound chain was minted alongside it (the
        // pre-0043 bug stranded a bound caller with cookie tokens it would never use plus this orphan).
        using var scope = RealAuth.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JournalRecallDbContext>();
        var active = await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync();

        active.ShouldNotBeEmpty();
        active.ShouldAllBe(t => t.BoundKeyThumbprint == key.Thumbprint);
        active.Select(t => t.ChainId).Distinct().Count().ShouldBe(1);
    }
}
