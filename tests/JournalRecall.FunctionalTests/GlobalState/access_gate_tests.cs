using System.Net;
using System.Net.Http.Json;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.GlobalState;

/// <summary>
/// Access gate &amp; public auth config (issue 0022, PRD-0001): the server 302s anonymous SPA navigations to
/// <c>/setup</c> on a fresh instance, else <c>/login</c>; allowlisted client routes pass through; and
/// <c>GET /api/auth/config</c> is reachable anonymously and reports <c>needsSetup</c>. Each test boots its
/// own host so the zero-Users precondition is deterministic.
/// </summary>
public class access_gate_tests : GlobalStateTestBase
{
    [Fact]
    public async Task anonymous_protected_navigation_redirects_to_setup_on_a_fresh_instance()
    {
        using var factory = new GlobalStateWebApplicationFactory();

        var response = await NoRedirect(factory).GetAsync("/app/sessions");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe("/app/setup");
    }

    [Fact]
    public async Task anonymous_protected_navigation_redirects_to_login_once_a_user_exists()
    {
        using var factory = new GlobalStateWebApplicationFactory();
        await factory.CreateClient().PostAsJsonAsync(ApiRoutes.Setup.Root, NewUser()); // a User now exists

        var response = await NoRedirect(factory).GetAsync("/app/sessions");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe("/app/login");
    }

    [Fact]
    public async Task allowlisted_public_routes_pass_through_for_anonymous_visitors()
    {
        using var factory = new GlobalStateWebApplicationFactory();

        // The gate admits the public routes (does not 302 to login/setup); the SPA shell it falls through
        // to is a Vite-build artifact, so we assert only the gate's admit decision here.
        foreach (var route in new[] { "/app/login", "/app/setup" })
            (await NoRedirect(factory).GetAsync(route)).StatusCode.ShouldNotBe(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task an_authenticated_visitor_is_admitted_past_the_gate()
    {
        using var factory = new GlobalStateWebApplicationFactory();
        var creds = NewUser();
        var client = NoRedirect(factory);
        await client.PostAsJsonAsync(ApiRoutes.Setup.Root, creds); // root Admin; works regardless of registration policy
        await client.PostAsJsonAsync(ApiRoutes.Auth.Login, creds);

        // Admitted (not redirected to login/setup); the shell itself is the Vite build's concern.
        (await client.GetAsync("/app/sessions")).StatusCode.ShouldNotBe(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task auth_config_is_anonymous_and_reports_needs_setup_flipping_false()
    {
        using var factory = new GlobalStateWebApplicationFactory();
        var client = factory.CreateClient();

        var before = (await client.GetFromJsonAsync<AuthConfig>(ApiRoutes.Auth.Config, HttpClientExtensions.Web))!;
        before.NeedsSetup.ShouldBeTrue();
        before.SelfRegistrationEnabled.ShouldBeFalse(); // closed by default (issue 0023)

        await client.PostAsJsonAsync(ApiRoutes.Setup.Root, NewUser());

        var after = (await client.GetFromJsonAsync<AuthConfig>(ApiRoutes.Auth.Config, HttpClientExtensions.Web))!;
        after.NeedsSetup.ShouldBeFalse();
    }
}
