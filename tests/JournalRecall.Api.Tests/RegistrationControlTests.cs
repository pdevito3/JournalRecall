using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using JournalRecall.Api.Domain.Identity;

namespace JournalRecall.Api.Tests;

/// <summary>
/// Operator-controlled registration (issue 0023, PRD-0001): closed by default; an Admin can open or
/// close it; the register API enforces it (403 off, Member on); and the access gate allowlists the
/// register route only when enabled. Each test boots its own factory so the app-wide toggle is isolated.
/// </summary>
public class RegistrationControlTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record Credentials(string Email, string Password);
    private sealed record AuthConfig(bool NeedsSetup, bool SelfRegistrationEnabled);
    private static Credentials NewUser() => new($"user-{Guid.NewGuid():N}@example.com", "Passw0rd!23");

    private static HttpClient NoRedirect(SkeletonWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>Setup creates the first User as the root Admin, then logs in.</summary>
    private static async Task<HttpClient> RootAdmin(SkeletonWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        var creds = NewUser();
        await client.PostAsJsonAsync("/api/setup", creds);
        await client.PostAsJsonAsync("/api/auth/login", creds);
        return client;
    }

    [Fact]
    public async Task A_new_instance_is_closed_by_default_and_register_is_forbidden()
    {
        using var factory = new ClosedRegistrationWebApplicationFactory();
        var client = factory.CreateClient();

        (await client.GetFromJsonAsync<AuthConfig>("/api/auth/config", Json))!.SelfRegistrationEnabled.ShouldBeFalse();
        (await client.PostAsJsonAsync("/api/auth/register", NewUser())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_admin_opens_registration_then_a_self_registrant_becomes_a_member()
    {
        using var factory = new ClosedRegistrationWebApplicationFactory();
        var admin = await RootAdmin(factory);

        (await admin.PutAsJsonAsync("/api/admin/registration", new { selfRegistrationEnabled = true }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var anon = factory.CreateClient();
        (await anon.GetFromJsonAsync<AuthConfig>("/api/auth/config", Json))!.SelfRegistrationEnabled.ShouldBeTrue();

        var creds = NewUser();
        (await anon.PostAsJsonAsync("/api/auth/register", creds)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        (await users.IsInRoleAsync((await users.FindByEmailAsync(creds.Email))!, Roles.Member)).ShouldBeTrue();
    }

    [Fact]
    public async Task An_admin_can_close_registration_again()
    {
        using var factory = new ClosedRegistrationWebApplicationFactory();
        var admin = await RootAdmin(factory);

        await admin.PutAsJsonAsync("/api/admin/registration", new { selfRegistrationEnabled = true });
        await admin.PutAsJsonAsync("/api/admin/registration", new { selfRegistrationEnabled = false });

        (await factory.CreateClient().PostAsJsonAsync("/api/auth/register", NewUser()))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_member_cannot_toggle_registration()
    {
        using var factory = new ClosedRegistrationWebApplicationFactory();
        var admin = await RootAdmin(factory);
        await admin.PutAsJsonAsync("/api/admin/registration", new { selfRegistrationEnabled = true });

        // A self-registered Member is forbidden from the Admin-only toggle.
        var memberCreds = NewUser();
        var member = factory.CreateClient();
        await member.PostAsJsonAsync("/api/auth/register", memberCreds);
        await member.PostAsJsonAsync("/api/auth/login", memberCreds);

        (await member.PutAsJsonAsync("/api/admin/registration", new { selfRegistrationEnabled = false }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_register_route_redirects_when_closed_and_passes_through_when_open()
    {
        using var factory = new ClosedRegistrationWebApplicationFactory();
        var admin = await RootAdmin(factory); // a User now exists → needsSetup false

        var closed = await NoRedirect(factory).GetAsync("/app/register");
        closed.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        closed.Headers.Location!.OriginalString.ShouldBe("/app/login");

        await admin.PutAsJsonAsync("/api/admin/registration", new { selfRegistrationEnabled = true });

        (await NoRedirect(factory).GetAsync("/app/register")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
