using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace JournalRecall.Api.Tests;

/// <summary>
/// End-to-end auth (ADR-0002): register → login mints a JWT delivered as a strict HttpOnly cookie;
/// the same token validates via cookie or Authorization: Bearer header; logout clears it.
/// </summary>
public class AuthTests : IClassFixture<SkeletonWebApplicationFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SkeletonWebApplicationFactory _factory;

    public AuthTests(SkeletonWebApplicationFactory factory) => _factory = factory;

    private sealed record Credentials(string Email, string Password);
    private sealed record UserDto(Guid Id, string Email);

    private static Credentials NewUser() => new($"user-{Guid.NewGuid():N}@example.com", "Passw0rd!");

    [Fact]
    public async Task Register_then_login_sets_an_httponly_auth_cookie()
    {
        var client = _factory.CreateClient();
        var creds = NewUser();

        (await client.PostAsJsonAsync("/api/auth/register", creds)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var login = await client.PostAsJsonAsync("/api/auth/login", creds);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        var setCookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("__Host-jr_auth="));
        setCookie.ShouldContain("httponly", Case.Insensitive); // not readable from document.cookie
        setCookie.ShouldContain("samesite=strict", Case.Insensitive);
    }

    [Fact]
    public async Task Me_returns_the_user_with_the_cookie_and_401_without_it()
    {
        var anon = _factory.CreateClient();
        (await anon.GetAsync("/api/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var client = _factory.CreateClient();
        var creds = NewUser();
        await client.PostAsJsonAsync("/api/auth/register", creds);
        await client.PostAsJsonAsync("/api/auth/login", creds); // cookie persisted by the client

        var me = await client.GetAsync("/api/me");
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await me.Content.ReadFromJsonAsync<UserDto>(Json);
        dto!.Email.ShouldBe(creds.Email);
    }

    [Fact]
    public async Task Me_succeeds_with_a_bearer_header_and_no_cookie()
    {
        var cookieClient = _factory.CreateClient();
        var creds = NewUser();
        await cookieClient.PostAsJsonAsync("/api/auth/register", creds);
        var login = await cookieClient.PostAsJsonAsync("/api/auth/login", creds);

        var token = ExtractToken(login);

        // A fresh client that does NOT carry cookies — proves header-based dual delivery.
        var bearerClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        bearerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var me = await bearerClient.GetAsync("/api/me");
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await me.Content.ReadFromJsonAsync<UserDto>(Json))!.Email.ShouldBe(creds.Email);
    }

    [Fact]
    public async Task Logout_clears_the_cookie_and_me_becomes_401()
    {
        var client = _factory.CreateClient();
        var creds = NewUser();
        await client.PostAsJsonAsync("/api/auth/register", creds);
        await client.PostAsJsonAsync("/api/auth/login", creds);
        (await client.GetAsync("/api/me")).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await client.PostAsync("/api/auth/logout", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.GetAsync("/api/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static string ExtractToken(HttpResponseMessage login)
    {
        var setCookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("__Host-jr_auth="));
        return setCookie["__Host-jr_auth=".Length..].Split(';')[0];
    }
}
