using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using JournalRecall.Api.Domain.Identity;

namespace JournalRecall.Api.Tests;

/// <summary>
/// Durable sessions end-to-end (issue 0019, ADR-0005): an expired access token is re-established by
/// rotating the refresh token; logout ends only the current device; Admin-disable revokes every
/// session; and the mobile body-flow returns tokens without cookies. (Password-change → revoke-all is
/// wired with its net-new change-own-password endpoint in issue 0024.)
/// </summary>
public class RefreshTokenTests : IClassFixture<SkeletonWebApplicationFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SkeletonWebApplicationFactory _factory;

    public RefreshTokenTests(SkeletonWebApplicationFactory factory) => _factory = factory;

    private sealed record Credentials(string Email, string Password);
    private sealed record TokenResponse(string AccessToken, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

    private static Credentials NewUser() => new($"user-{Guid.NewGuid():N}@example.com", "Passw0rd!");

    [Fact]
    public async Task Refresh_with_the_cookie_reestablishes_access_and_rotates_the_refresh_token()
    {
        var client = _factory.CreateClient();
        var creds = NewUser();
        await client.PostAsJsonAsync("/api/auth/register", creds);
        var login = await client.PostAsJsonAsync("/api/auth/login", creds);
        var oldRefresh = CookieValue(login, "__Secure-jr_refresh");

        var refresh = await client.PostAsync("/api/auth/refresh", null);
        refresh.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Both cookies are re-issued; the refresh token is rotated to a new value.
        refresh.Headers.GetValues("Set-Cookie").ShouldContain(c => c.StartsWith("__Host-jr_auth="));
        CookieValue(refresh, "__Secure-jr_refresh").ShouldNotBe(oldRefresh);

        // Access is genuinely re-established with the rotated cookies.
        (await client.GetAsync("/api/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_revokes_only_the_current_device()
    {
        var creds = NewUser();
        await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", creds);

        var deviceA = _factory.CreateClient();
        var deviceB = _factory.CreateClient();
        var refreshA = CookieValue(await deviceA.PostAsJsonAsync("/api/auth/login", creds), "__Secure-jr_refresh");
        var refreshB = CookieValue(await deviceB.PostAsJsonAsync("/api/auth/login", creds), "__Secure-jr_refresh");

        (await deviceA.PostAsync("/api/auth/logout", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Device A's token is dead; Device B's session is untouched. Use the body-flow so the assertion
        // is about the tokens themselves, independent of each client's cleared cookies.
        (await Refresh(refreshA)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Refresh(refreshB)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Disabling_a_user_revokes_all_their_sessions()
    {
        var creds = NewUser();
        var member = _factory.CreateClient();
        await member.PostAsJsonAsync("/api/auth/register", creds);
        var refresh = CookieValue(await member.PostAsJsonAsync("/api/auth/login", creds), "__Secure-jr_refresh");

        var admin = await AdminClient();
        var target = (await admin.GetFromJsonAsync<List<AdminUserDto>>("/api/admin/users", Json))!
            .Single(u => u.Email == creds.Email);
        (await admin.PostAsync($"/api/admin/users/{target.Id}/disable", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await Refresh(refresh)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_mobile_body_flow_returns_rotated_tokens_without_cookies()
    {
        var cookieClient = _factory.CreateClient();
        var creds = NewUser();
        await cookieClient.PostAsJsonAsync("/api/auth/register", creds);
        var refresh = CookieValue(await cookieClient.PostAsJsonAsync("/api/auth/login", creds), "__Secure-jr_refresh");

        var response = await Refresh(refresh);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse(); // body-flow sets no cookies

        var body = (await response.Content.ReadFromJsonAsync<TokenResponse>(Json))!;
        body.AccessToken.ShouldNotBeNullOrEmpty();
        body.RefreshToken.ShouldNotBeNullOrEmpty();
        body.RefreshToken.ShouldNotBe(refresh); // rotated
    }

    private sealed record AdminUserDto(Guid Id, string Email, List<string> Roles, bool IsDisabled);

    /// <summary>A cookie-less client posts the refresh token in the body (the mobile flow).</summary>
    private Task<HttpResponseMessage> Refresh(string refreshToken)
    {
        var mobile = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });
        return mobile.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
    }

    private async Task<HttpClient> AdminClient()
    {
        var creds = NewUser();
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", creds);
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            await users.AddToRoleAsync((await users.FindByEmailAsync(creds.Email))!, Roles.Admin);
        }
        await client.PostAsJsonAsync("/api/auth/login", creds);
        return client;
    }

    private static string CookieValue(HttpResponseMessage response, string name)
    {
        var setCookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(name + "="));
        return setCookie[(name.Length + 1)..].Split(';')[0];
    }
}
