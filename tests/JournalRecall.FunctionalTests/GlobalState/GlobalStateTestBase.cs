using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.GlobalState;

/// <summary>
/// Shared helpers for app-global functional tests (TEST-0012). Each test boots its own
/// <see cref="GlobalStateWebApplicationFactory"/> so the zero-Users / closed-registration precondition is
/// deterministic; the functional assembly runs serially, so these never collide with the pooled host.
/// </summary>
public abstract class GlobalStateTestBase
{
    protected const string Password = "Passw0rd!23";

    protected sealed record Credentials(string Username, string Password);
    protected sealed record AuthConfig(bool NeedsSetup, bool SelfRegistrationEnabled);

    protected static Credentials NewUser() => new($"user_{Guid.NewGuid():N}"[..18], Password);

    /// <summary>A client that does not follow redirects (so the gate's 302 is observable). Addressed over
    /// https so the Secure auth cookie is sent (custom client options otherwise reset the base address).</summary>
    protected static HttpClient NoRedirect(GlobalStateWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

    /// <summary>Setup creates the first User as the root Admin, then logs in.</summary>
    protected static async Task<HttpClient> RootAdmin(GlobalStateWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        var creds = NewUser();
        await client.PostAsJsonAsync(ApiRoutes.Setup.Root, creds);
        await client.PostAsJsonAsync(ApiRoutes.Auth.Login, creds);
        return client;
    }
}
