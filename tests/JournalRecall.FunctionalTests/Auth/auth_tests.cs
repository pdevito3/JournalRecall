using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Auth;

/// <summary>
/// End-to-end auth (ADR-0002): register → login mints a JWT delivered as a strict HttpOnly cookie; the
/// same token validates via cookie or Authorization: Bearer header; logout clears it.
/// </summary>
public class auth_tests(WebTestFixture fixture) : AuthTestBase(fixture)
{
    private sealed record UserDto(Guid Id, string Email);

    [Fact]
    public async Task register_then_login_sets_an_httponly_auth_cookie()
    {
        var client = RealAuth.CreateClient();
        var creds = NewUser();

        (await client.PostAsJsonAsync(ApiRoutes.Auth.Register, creds)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var login = await client.PostAsJsonAsync(ApiRoutes.Auth.Login, creds);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        var setCookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("__Host-jr_auth="));
        setCookie.ShouldContain("httponly", Case.Insensitive); // not readable from document.cookie
        setCookie.ShouldContain("samesite=strict", Case.Insensitive);
    }

    [Fact]
    public async Task me_returns_the_user_with_the_cookie_and_401_without_it()
    {
        var anon = RealAuth.CreateClient();
        (await anon.GetAsync(ApiRoutes.Me)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var client = await RealAuth.CreateAuthenticatedClientAsync();
        var me = await client.GetAsync(ApiRoutes.Me);
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await me.ReadJsonAsync<UserDto>())!.Email.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task me_succeeds_with_a_bearer_header_and_no_cookie()
    {
        var cookieClient = RealAuth.CreateClient();
        var creds = NewUser();
        await cookieClient.PostAsJsonAsync(ApiRoutes.Auth.Register, creds);
        var login = await cookieClient.PostAsJsonAsync(ApiRoutes.Auth.Login, creds);
        var token = CookieValue(login, "__Host-jr_auth");

        // A fresh client that does NOT carry cookies — proves header-based dual delivery.
        var bearerClient = RealAuth.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        bearerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var me = await bearerClient.GetAsync(ApiRoutes.Me);
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await me.ReadJsonAsync<UserDto>())!.Email.ShouldBe(creds.Email);
    }

    [Fact]
    public async Task logout_clears_the_cookie_and_me_becomes_401()
    {
        var client = await RealAuth.CreateAuthenticatedClientAsync();
        (await client.GetAsync(ApiRoutes.Me)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await client.PostAsync(ApiRoutes.Auth.Logout, null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.GetAsync(ApiRoutes.Me)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
