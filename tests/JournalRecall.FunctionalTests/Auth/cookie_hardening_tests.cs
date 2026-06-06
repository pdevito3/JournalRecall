using System.Net;
using System.Net.Http.Json;
using JournalRecall.Api.Auth;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Auth;

/// <summary>
/// Cookie hardening + CSRF (issue 0020, ADR-0005): the auth cookies carry the <c>__Host-</c>/<c>__Secure-</c>
/// prefixes, <c>HttpOnly</c>, <c>Secure</c>, and <c>SameSite=Strict</c>, with the refresh cookie path-scoped
/// to the refresh endpoint; and mutating <c>/api</c> requests are rejected without an <c>X-CSRF</c> header.
/// </summary>
public class cookie_hardening_tests(WebTestFixture fixture) : AuthTestBase(fixture)
{
    [Fact]
    public async Task auth_cookies_are_prefix_hardened_httponly_secure_samesite_strict_and_path_scoped()
    {
        var client = RealAuth.CreateClient();
        var creds = NewUser();
        await client.PostAsJsonAsync(ApiRoutes.Auth.Register, creds);
        var login = await client.PostAsJsonAsync(ApiRoutes.Auth.Login, creds);

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
    public async Task a_mutating_request_is_rejected_without_the_csrf_header_and_succeeds_with_it()
    {
        var creds = NewUser();

        var noCsrf = RealAuth.CreateClient();
        noCsrf.DefaultRequestHeaders.Remove(CsrfMiddleware.HeaderName);
        (await noCsrf.PostAsJsonAsync(ApiRoutes.Auth.Register, creds)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Safe methods are unaffected by the CSRF gate.
        (await noCsrf.GetAsync(ApiRoutes.Health)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var withCsrf = RealAuth.CreateClient(); // carries X-CSRF by default (see the factory)
        (await withCsrf.PostAsJsonAsync(ApiRoutes.Auth.Register, creds)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
