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
/// Temporary passwords &amp; forced change (issue 0024, PRD-0001): an Admin onboards/recovers a User with
/// a temporary password the User must replace on first sign-in. While the flag is set the server returns
/// 403 password_change_required outside a small allowlist; setting a new password clears the flag, keeps
/// the User signed in here, and revokes their other sessions; an Admin reset re-arms the flag.
/// </summary>
public class ForcedPasswordChangeTests : IClassFixture<SkeletonWebApplicationFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SkeletonWebApplicationFactory _factory;

    public ForcedPasswordChangeTests(SkeletonWebApplicationFactory factory) => _factory = factory;

    private sealed record Credentials(string Email, string Password);
    private sealed record AdminUserDto(Guid Id, string Email, List<string> Roles, bool IsDisabled);
    private sealed record UserDto(Guid Id, string Email, List<string> Roles, bool MustChangePassword);
    private static Credentials NewUser() => new($"user-{Guid.NewGuid():N}@example.com", "Passw0rd!23");

    private async Task<HttpClient> AdminClient()
    {
        var creds = NewUser();
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", creds);
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            await users.AddToRoleAsync((await users.FindByEmailAsync(creds.Email))!, Roles.Admin);
        }
        await client.PostAsJsonAsync("/api/auth/login", creds);
        return client;
    }

    private async Task<Credentials> AdminCreatesUser(HttpClient admin, string role)
    {
        var creds = new Credentials($"user-{Guid.NewGuid():N}@example.com", "TempPass123");
        var created = await admin.PostAsJsonAsync("/api/admin/users", new { email = creds.Email, password = creds.Password, role });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        return creds;
    }

    [Fact]
    public async Task An_admin_creates_a_user_with_a_temp_password_and_an_assigned_role()
    {
        var admin = await AdminClient();
        var created = await admin.PostAsJsonAsync("/api/admin/users",
            new { email = $"user-{Guid.NewGuid():N}@example.com", password = "TempPass123", role = "Admin" });

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var dto = (await created.Content.ReadFromJsonAsync<AdminUserDto>(Json))!;
        dto.Roles.ShouldBe(["Admin"]);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        (await users.FindByIdAsync(dto.Id.ToString()))!.MustChangePassword.ShouldBeTrue();
    }

    [Fact]
    public async Task A_forced_change_user_is_blocked_until_they_set_a_new_password()
    {
        var admin = await AdminClient();
        var creds = await AdminCreatesUser(admin, "Member");

        var user = _factory.CreateClient();
        (await user.PostAsJsonAsync("/api/auth/login", creds)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Blocked outside the allowlist with the sentinel reason.
        var blocked = await user.GetAsync("/api/sessions");
        blocked.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await blocked.Content.ReadAsStringAsync()).ShouldContain("password_change_required");

        // Allowlisted: /me reports the forced-change state so the SPA can confine the User.
        var me = (await user.GetFromJsonAsync<UserDto>("/api/me", Json))!;
        me.MustChangePassword.ShouldBeTrue();

        // Setting a new password clears the flag and drops the User into the app.
        (await user.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = creds.Password, newPassword = "BrandNewPass1" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await user.GetAsync("/api/sessions")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await user.GetFromJsonAsync<UserDto>("/api/me", Json))!.MustChangePassword.ShouldBeFalse();
    }

    [Fact]
    public async Task Changing_the_password_revokes_the_users_other_sessions()
    {
        var admin = await AdminClient();
        var creds = await AdminCreatesUser(admin, "Member");

        var deviceA = _factory.CreateClient();
        var deviceB = _factory.CreateClient();
        await deviceA.PostAsJsonAsync("/api/auth/login", creds);
        var refreshB = CookieValue(await deviceB.PostAsJsonAsync("/api/auth/login", creds), "__Secure-jr_refresh");

        (await deviceA.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = creds.Password, newPassword = "BrandNewPass1" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Device B's session is revoked; device A stays signed in here.
        (await RefreshWithBody(refreshB)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await deviceA.GetAsync("/api/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_admin_reset_puts_a_user_back_into_forced_change()
    {
        var admin = await AdminClient();
        var creds = await AdminCreatesUser(admin, "Member");

        // The User clears the initial temp password.
        var user = _factory.CreateClient();
        await user.PostAsJsonAsync("/api/auth/login", creds);
        await user.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = creds.Password, newPassword = "ChosenPass123" });
        (await user.GetFromJsonAsync<UserDto>("/api/me", Json))!.MustChangePassword.ShouldBeFalse();

        // Admin resets to a new temp password.
        var target = (await admin.GetFromJsonAsync<List<AdminUserDto>>("/api/admin/users", Json))!
            .Single(u => u.Email == creds.Email);
        (await admin.PostAsJsonAsync($"/api/admin/users/{target.Id}/reset-password", new { password = "ResetTemp123" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Logging in with the new temp password lands back in the forced-change state.
        var again = _factory.CreateClient();
        await again.PostAsJsonAsync("/api/auth/login", new Credentials(creds.Email, "ResetTemp123"));
        (await again.GetFromJsonAsync<UserDto>("/api/me", Json))!.MustChangePassword.ShouldBeTrue();
        (await again.GetAsync("/api/sessions")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private Task<HttpResponseMessage> RefreshWithBody(string refreshToken)
    {
        var mobile = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        return mobile.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
    }

    private static string CookieValue(HttpResponseMessage response, string name)
    {
        var setCookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(name + "="));
        return setCookie[(name.Length + 1)..].Split(';')[0];
    }
}
