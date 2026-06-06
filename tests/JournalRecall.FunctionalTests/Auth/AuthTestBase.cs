using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using JournalRecall.Api.Domain.Identity;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Auth;

/// <summary>
/// Shared helpers for the auth-behavior functional tests (TEST-0011). These always exercise <b>real auth</b>
/// — register/login/refresh against the real pipeline; never the fake-auth scheme.
/// </summary>
public abstract class AuthTestBase(WebTestFixture fixture) : TestBase(fixture)
{
    protected const string Password = TestingWebApplicationFactory.DefaultPassword;

    protected sealed record Credentials(string Username, string Password);

    protected static Credentials NewUser() => new($"user_{Guid.NewGuid():N}"[..18], Password);

    /// <summary>Reads a Set-Cookie value by cookie name from a response.</summary>
    protected static string CookieValue(HttpResponseMessage response, string name)
    {
        var setCookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(name + "="));
        return setCookie[(name.Length + 1)..].Split(';')[0];
    }

    /// <summary>A cookie-less client posts the refresh token in the body (the mobile flow).</summary>
    protected Task<HttpResponseMessage> RefreshWithBody(string refreshToken)
    {
        var mobile = RealAuth.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        return mobile.PostAsJsonAsync(ApiRoutes.Auth.Refresh, new { refreshToken });
    }

    /// <summary>Registers a fresh User (optionally elevated to Admin via UserManager) and signs them in.</summary>
    protected async Task<HttpClient> RegisterAndLogin(Credentials creds, bool admin = false)
    {
        var client = RealAuth.CreateClient();
        await client.PostAsJsonAsync(ApiRoutes.Auth.Register, creds);
        if (admin)
        {
            using var scope = RealAuth.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            await users.AddToRoleAsync((await users.FindByNameAsync(creds.Username))!, Roles.Admin);
        }
        // Log in after any promotion so the minted JWT carries the right role claims.
        await client.PostAsJsonAsync(ApiRoutes.Auth.Login, creds);
        return client;
    }

    /// <summary>A signed-in Admin client.</summary>
    protected Task<HttpClient> AdminClient() => RegisterAndLogin(NewUser(), admin: true);
}
