using System.Net.Http.Json;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests;

/// <summary>
/// The default functional factory: <b>real auth</b> (PRD-0003). <see cref="CreateAuthenticatedClientAsync"/>
/// runs the genuine register→login flow and returns an <see cref="HttpClient"/> carrying the real auth
/// cookie + <c>X-CSRF</c> header, so the production auth pipeline is exercised end-to-end.
/// </summary>
public sealed class TestingWebApplicationFactory : FunctionalWebApplicationFactory
{
    /// <summary>The password every test User registers with (meets the 10-char NIST-aligned policy).</summary>
    public const string DefaultPassword = "Passw0rd!23";

    /// <summary>Registers + logs in a fresh User; the returned client carries its cookie and X-CSRF header.</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(string? username = null, string? password = null)
    {
        username ??= $"user_{Guid.NewGuid():N}"[..18];
        password ??= DefaultPassword;

        var client = CreateClient();
        var register = await client.PostAsJsonAsync(ApiRoutes.Auth.Register, new { username, password });
        register.EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync(ApiRoutes.Auth.Login, new { username, password });
        login.EnsureSuccessStatusCode();
        return client;
    }
}
