using System.Net;
using System.Net.Http.Json;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Auth;

/// <summary>
/// Durable sessions end-to-end (issue 0019, ADR-0005): an expired access token is re-established by
/// rotating the refresh token; logout ends only the current device; Admin-disable revokes every session;
/// and the mobile body-flow returns tokens without cookies.
/// </summary>
public class refresh_token_tests(WebTestFixture fixture) : AuthTestBase(fixture)
{
    private sealed record TokenResponse(string AccessToken, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);
    private sealed record AdminUserDto(Guid Id, string Email, List<string> Roles, bool IsDisabled);

    [Fact]
    public async Task refresh_with_the_cookie_reestablishes_access_and_rotates_the_refresh_token()
    {
        var client = RealAuth.CreateClient();
        var creds = NewUser();
        await client.PostAsJsonAsync(ApiRoutes.Auth.Register, creds);
        var login = await client.PostAsJsonAsync(ApiRoutes.Auth.Login, creds);
        var oldRefresh = CookieValue(login, "__Secure-jr_refresh");

        var refresh = await client.PostAsync(ApiRoutes.Auth.Refresh, null);
        refresh.StatusCode.ShouldBe(HttpStatusCode.OK);

        refresh.Headers.GetValues("Set-Cookie").ShouldContain(c => c.StartsWith("__Host-jr_auth="));
        CookieValue(refresh, "__Secure-jr_refresh").ShouldNotBe(oldRefresh);

        (await client.GetAsync(ApiRoutes.Me)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task logout_revokes_only_the_current_device()
    {
        var creds = NewUser();
        await RealAuth.CreateClient().PostAsJsonAsync(ApiRoutes.Auth.Register, creds);

        var deviceA = RealAuth.CreateClient();
        var deviceB = RealAuth.CreateClient();
        var refreshA = CookieValue(await deviceA.PostAsJsonAsync(ApiRoutes.Auth.Login, creds), "__Secure-jr_refresh");
        var refreshB = CookieValue(await deviceB.PostAsJsonAsync(ApiRoutes.Auth.Login, creds), "__Secure-jr_refresh");

        (await deviceA.PostAsync(ApiRoutes.Auth.Logout, null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Device A's token is dead; Device B's session is untouched (asserted via the body-flow).
        (await RefreshWithBody(refreshA)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await RefreshWithBody(refreshB)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task disabling_a_user_revokes_all_their_sessions()
    {
        var creds = NewUser();
        var member = RealAuth.CreateClient();
        await member.PostAsJsonAsync(ApiRoutes.Auth.Register, creds);
        var refresh = CookieValue(await member.PostAsJsonAsync(ApiRoutes.Auth.Login, creds), "__Secure-jr_refresh");

        var admin = await AdminClient();
        var target = (await admin.GetFromJsonAsync<List<AdminUserDto>>(ApiRoutes.Admin.Users, HttpClientExtensions.Web))!
            .Single(u => u.Email == creds.Email);
        (await admin.PostAsync(ApiRoutes.Admin.Disable(target.Id), null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await RefreshWithBody(refresh)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task the_mobile_body_flow_returns_rotated_tokens_without_cookies()
    {
        var cookieClient = RealAuth.CreateClient();
        var creds = NewUser();
        await cookieClient.PostAsJsonAsync(ApiRoutes.Auth.Register, creds);
        var refresh = CookieValue(await cookieClient.PostAsJsonAsync(ApiRoutes.Auth.Login, creds), "__Secure-jr_refresh");

        var response = await RefreshWithBody(refresh);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse(); // body-flow sets no cookies

        var body = (await response.ReadJsonAsync<TokenResponse>())!;
        body.AccessToken.ShouldNotBeNullOrEmpty();
        body.RefreshToken.ShouldNotBeNullOrEmpty();
        body.RefreshToken.ShouldNotBe(refresh); // rotated
    }
}
