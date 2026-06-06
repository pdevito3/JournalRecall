using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using JournalRecall.Api.Domain.Identity;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Auth;

/// <summary>
/// Temporary passwords &amp; forced change (issue 0024, PRD-0001): an Admin onboards/recovers a User with a
/// temporary password the User must replace on first sign-in. While the flag is set the server returns 403
/// password_change_required outside a small allowlist; setting a new password clears the flag, keeps the
/// User signed in here, and revokes their other sessions; an Admin reset re-arms the flag.
/// </summary>
public class forced_password_change_tests(WebTestFixture fixture) : AuthTestBase(fixture)
{
    private sealed record AdminUserDto(Guid Id, string Email, List<string> Roles, bool IsDisabled);
    private sealed record UserDto(Guid Id, string Email, List<string> Roles, bool MustChangePassword);

    private async Task<Credentials> AdminCreatesUser(HttpClient admin, string role)
    {
        var creds = new Credentials($"user-{Guid.NewGuid():N}@example.com", "TempPass123");
        var created = await admin.PostJsonAsync(ApiRoutes.Admin.Users, new { email = creds.Email, password = creds.Password, role });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        return creds;
    }

    [Fact]
    public async Task an_admin_creates_a_user_with_a_temp_password_and_an_assigned_role()
    {
        var admin = await AdminClient();
        var created = await admin.PostJsonAsync(ApiRoutes.Admin.Users,
            new { email = $"user-{Guid.NewGuid():N}@example.com", password = "TempPass123", role = "Admin" });

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var dto = (await created.ReadJsonAsync<AdminUserDto>())!;
        dto.Roles.ShouldBe(["Admin"]);

        using var scope = RealAuth.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        (await users.FindByIdAsync(dto.Id.ToString()))!.MustChangePassword.ShouldBeTrue();
    }

    [Fact]
    public async Task a_forced_change_user_is_blocked_until_they_set_a_new_password()
    {
        var admin = await AdminClient();
        var creds = await AdminCreatesUser(admin, "Member");

        var user = RealAuth.CreateClient();
        (await user.PostJsonAsync(ApiRoutes.Auth.Login, creds)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Blocked outside the allowlist with the sentinel reason.
        var blocked = await user.GetAsync(ApiRoutes.Sessions.Root);
        blocked.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await blocked.Content.ReadAsStringAsync()).ShouldContain("password_change_required");

        // Allowlisted: /me reports the forced-change state so the SPA can confine the User.
        (await user.GetFromJsonAsync<UserDto>(ApiRoutes.Me, HttpClientExtensions.Web))!.MustChangePassword.ShouldBeTrue();

        // Setting a new password clears the flag and drops the User into the app.
        (await user.PostJsonAsync(ApiRoutes.Auth.ChangePassword,
            new { currentPassword = creds.Password, newPassword = "BrandNewPass1" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await user.GetAsync(ApiRoutes.Sessions.Root)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await user.GetFromJsonAsync<UserDto>(ApiRoutes.Me, HttpClientExtensions.Web))!.MustChangePassword.ShouldBeFalse();
    }

    [Fact]
    public async Task changing_the_password_revokes_the_users_other_sessions()
    {
        var admin = await AdminClient();
        var creds = await AdminCreatesUser(admin, "Member");

        var deviceA = RealAuth.CreateClient();
        var deviceB = RealAuth.CreateClient();
        await deviceA.PostJsonAsync(ApiRoutes.Auth.Login, creds);
        var refreshB = CookieValue(await deviceB.PostJsonAsync(ApiRoutes.Auth.Login, creds), "__Secure-jr_refresh");

        (await deviceA.PostJsonAsync(ApiRoutes.Auth.ChangePassword,
            new { currentPassword = creds.Password, newPassword = "BrandNewPass1" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Device B's session is revoked; device A stays signed in here.
        (await RefreshWithBody(refreshB)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await deviceA.GetAsync(ApiRoutes.Me)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task an_admin_reset_puts_a_user_back_into_forced_change()
    {
        var admin = await AdminClient();
        var creds = await AdminCreatesUser(admin, "Member");

        // The User clears the initial temp password.
        var user = RealAuth.CreateClient();
        await user.PostJsonAsync(ApiRoutes.Auth.Login, creds);
        await user.PostJsonAsync(ApiRoutes.Auth.ChangePassword,
            new { currentPassword = creds.Password, newPassword = "ChosenPass123" });
        (await user.GetFromJsonAsync<UserDto>(ApiRoutes.Me, HttpClientExtensions.Web))!.MustChangePassword.ShouldBeFalse();

        // Admin resets to a new temp password.
        var target = (await admin.GetFromJsonAsync<List<AdminUserDto>>(ApiRoutes.Admin.Users, HttpClientExtensions.Web))!
            .Single(u => u.Email == creds.Email);
        (await admin.PostJsonAsync(ApiRoutes.Admin.ResetPassword(target.Id), new { password = "ResetTemp123" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Logging in with the new temp password lands back in the forced-change state.
        var again = RealAuth.CreateClient();
        await again.PostJsonAsync(ApiRoutes.Auth.Login, new Credentials(creds.Email, "ResetTemp123"));
        (await again.GetFromJsonAsync<UserDto>(ApiRoutes.Me, HttpClientExtensions.Web))!.MustChangePassword.ShouldBeTrue();
        (await again.GetAsync(ApiRoutes.Sessions.Root)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
