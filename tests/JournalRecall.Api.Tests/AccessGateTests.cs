using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace JournalRecall.Api.Tests;

/// <summary>
/// Access gate &amp; public auth config (issue 0022, PRD-0001): the server 302s anonymous SPA
/// navigations to <c>/setup</c> on a fresh instance, else <c>/login</c>; allowlisted client routes pass
/// through; and <c>GET /api/auth/config</c> is reachable anonymously and reports <c>needsSetup</c>.
/// Each test boots its own factory so the zero-Users precondition is deterministic.
/// </summary>
public class AccessGateTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record Credentials(string Email, string Password);
    private sealed record AuthConfig(bool NeedsSetup, bool SelfRegistrationEnabled);
    private static Credentials NewUser() => new($"user-{Guid.NewGuid():N}@example.com", "Passw0rd!23");

    private static HttpClient NoRedirect(SkeletonWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Anonymous_protected_navigation_redirects_to_setup_on_a_fresh_instance()
    {
        using var factory = new SkeletonWebApplicationFactory();

        var response = await NoRedirect(factory).GetAsync("/app/sessions");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe("/app/setup");
    }

    [Fact]
    public async Task Anonymous_protected_navigation_redirects_to_login_once_a_user_exists()
    {
        using var factory = new SkeletonWebApplicationFactory();
        await factory.CreateClient().PostAsJsonAsync("/api/auth/register", NewUser());

        var response = await NoRedirect(factory).GetAsync("/app/sessions");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe("/app/login");
    }

    [Fact]
    public async Task Allowlisted_public_routes_pass_through_for_anonymous_visitors()
    {
        using var factory = new SkeletonWebApplicationFactory();

        foreach (var route in new[] { "/app/login", "/app/setup" })
            (await NoRedirect(factory).GetAsync(route)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_authenticated_visitor_is_served_the_app_shell()
    {
        using var factory = new SkeletonWebApplicationFactory();
        var creds = NewUser();
        var client = factory.CreateClient(); // cookies handled, https base
        await client.PostAsJsonAsync("/api/auth/register", creds);
        await client.PostAsJsonAsync("/api/auth/login", creds);

        (await client.GetAsync("/app/sessions")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Auth_config_is_anonymous_and_reports_needs_setup_flipping_false()
    {
        using var factory = new SkeletonWebApplicationFactory();
        var client = factory.CreateClient();

        var before = (await client.GetFromJsonAsync<AuthConfig>("/api/auth/config", Json))!;
        before.NeedsSetup.ShouldBeTrue();
        before.SelfRegistrationEnabled.ShouldBeFalse();

        await client.PostAsJsonAsync("/api/auth/register", NewUser());

        var after = (await client.GetFromJsonAsync<AuthConfig>("/api/auth/config", Json))!;
        after.NeedsSetup.ShouldBeFalse();
    }
}
