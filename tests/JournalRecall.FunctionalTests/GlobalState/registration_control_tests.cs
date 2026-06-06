using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using JournalRecall.Api.Domain.Identity;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.GlobalState;

/// <summary>
/// Operator-controlled registration (issue 0023, PRD-0001): closed by default; an Admin can open or close
/// it; the register API enforces it (403 off, Member on); and the access gate allowlists the register route
/// only when enabled. Each test boots its own host so the app-wide toggle is isolated.
/// </summary>
public class registration_control_tests : GlobalStateTestBase
{
    [Fact]
    public async Task a_new_instance_is_closed_by_default_and_register_is_forbidden()
    {
        using var factory = new GlobalStateWebApplicationFactory();
        var client = factory.CreateClient();

        (await client.GetFromJsonAsync<AuthConfig>(ApiRoutes.Auth.Config, HttpClientExtensions.Web))!
            .SelfRegistrationEnabled.ShouldBeFalse();
        (await client.PostAsJsonAsync(ApiRoutes.Auth.Register, NewUser())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task an_admin_opens_registration_then_a_self_registrant_becomes_a_member()
    {
        using var factory = new GlobalStateWebApplicationFactory();
        var admin = await RootAdmin(factory);

        (await admin.PutAsJsonAsync(ApiRoutes.Admin.Registration, new { selfRegistrationEnabled = true }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var anon = factory.CreateClient();
        (await anon.GetFromJsonAsync<AuthConfig>(ApiRoutes.Auth.Config, HttpClientExtensions.Web))!
            .SelfRegistrationEnabled.ShouldBeTrue();

        var creds = NewUser();
        (await anon.PostAsJsonAsync(ApiRoutes.Auth.Register, creds)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        (await users.IsInRoleAsync((await users.FindByEmailAsync(creds.Email))!, Roles.Member)).ShouldBeTrue();
    }

    [Fact]
    public async Task an_admin_can_close_registration_again()
    {
        using var factory = new GlobalStateWebApplicationFactory();
        var admin = await RootAdmin(factory);

        await admin.PutAsJsonAsync(ApiRoutes.Admin.Registration, new { selfRegistrationEnabled = true });
        await admin.PutAsJsonAsync(ApiRoutes.Admin.Registration, new { selfRegistrationEnabled = false });

        (await factory.CreateClient().PostAsJsonAsync(ApiRoutes.Auth.Register, NewUser()))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task a_member_cannot_toggle_registration()
    {
        using var factory = new GlobalStateWebApplicationFactory();
        var admin = await RootAdmin(factory);
        await admin.PutAsJsonAsync(ApiRoutes.Admin.Registration, new { selfRegistrationEnabled = true });

        var memberCreds = NewUser();
        var member = factory.CreateClient();
        await member.PostAsJsonAsync(ApiRoutes.Auth.Register, memberCreds);
        await member.PostAsJsonAsync(ApiRoutes.Auth.Login, memberCreds);

        (await member.PutAsJsonAsync(ApiRoutes.Admin.Registration, new { selfRegistrationEnabled = false }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task the_register_route_redirects_when_closed_and_passes_through_when_open()
    {
        using var factory = new GlobalStateWebApplicationFactory();
        var admin = await RootAdmin(factory); // a User now exists → needsSetup false

        var closed = await NoRedirect(factory).GetAsync("/app/register");
        closed.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        closed.Headers.Location!.OriginalString.ShouldBe("/app/login");

        await admin.PutAsJsonAsync(ApiRoutes.Admin.Registration, new { selfRegistrationEnabled = true });

        // Now open: the gate admits /app/register (no longer 302s to login); the served shell is a Vite
        // build artifact, so assert only the gate's admit decision.
        (await NoRedirect(factory).GetAsync("/app/register")).StatusCode.ShouldNotBe(HttpStatusCode.Redirect);
    }
}
