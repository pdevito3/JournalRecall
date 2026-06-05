using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace JournalRecall.Api.Tests;

/// <summary>
/// Cookie hardening + CSRF (issue 0020, ADR-0005): the auth cookies carry the <c>__Host-</c>/<c>__Secure-</c>
/// prefixes, <c>HttpOnly</c>, <c>Secure</c>, and <c>SameSite=Strict</c>, with the refresh cookie
/// path-scoped to the refresh endpoint; and mutating <c>/api</c> requests are rejected without an
/// <c>X-CSRF</c> header. (The TestServer is addressed over https — see the factory — so Secure cookies
/// flow, mirroring Aspire's TLS dev boot.)
/// </summary>
public class CookieHardeningTests : IClassFixture<SkeletonWebApplicationFactory>
{
    private readonly SkeletonWebApplicationFactory _factory;

    public CookieHardeningTests(SkeletonWebApplicationFactory factory) => _factory = factory;

    private sealed record Credentials(string Email, string Password);
    private static Credentials NewUser() => new($"user-{Guid.NewGuid():N}@example.com", "Passw0rd!23");

    [Fact]
    public async Task Auth_cookies_are_prefix_hardened_httponly_secure_samesite_strict_and_path_scoped()
    {
        var client = _factory.CreateClient();
        var creds = NewUser();
        await client.PostAsJsonAsync("/api/auth/register", creds);
        var login = await client.PostAsJsonAsync("/api/auth/login", creds);

        var access = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("__Host-jr_auth="));
        access.ShouldContain("secure", Case.Insensitive);
        access.ShouldContain("httponly", Case.Insensitive);
        access.ShouldContain("samesite=strict", Case.Insensitive);
        access.ShouldContain("path=/", Case.Insensitive);

        var refresh = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("__Secure-jr_refresh="));
        refresh.ShouldContain("secure", Case.Insensitive);
        refresh.ShouldContain("httponly", Case.Insensitive);
        refresh.ShouldContain("samesite=strict", Case.Insensitive);
        refresh.ShouldContain("path=/api/auth/refresh", Case.Insensitive); // only ever sent to refresh
    }

    [Fact]
    public async Task A_mutating_request_is_rejected_without_the_csrf_header_and_succeeds_with_it()
    {
        var creds = NewUser();

        var noCsrf = _factory.CreateClient();
        noCsrf.DefaultRequestHeaders.Remove("X-CSRF");
        (await noCsrf.PostAsJsonAsync("/api/auth/register", creds)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Safe methods are unaffected by the CSRF gate.
        (await noCsrf.GetAsync("/api/health")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var withCsrf = _factory.CreateClient(); // carries X-CSRF by default (see the factory)
        (await withCsrf.PostAsJsonAsync("/api/auth/register", creds)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
