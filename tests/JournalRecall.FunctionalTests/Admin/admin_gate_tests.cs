using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using JournalRecall.Api.Domain.Identity;
using JournalRecall.FunctionalTests.Auth;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Admin;

/// <summary>
/// Roles + admin gate (issue 0003): the admin endpoint requires the CanAccessAdmin permission, which only
/// the Admin role grants; anonymous is 401, Member is 403, Admin is 200; new registrations are Member.
/// </summary>
public class admin_gate_tests(WebTestFixture fixture) : AuthTestBase(fixture)
{
    [Fact]
    public async Task anonymous_caller_gets_401()
    {
        (await RealAuth.CreateClient().GetAsync(ApiRoutes.Admin.Ping)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task member_gets_403()
    {
        var client = await RegisterAndLogin(NewUser());
        (await client.GetAsync(ApiRoutes.Admin.Ping)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task admin_gets_200()
    {
        var client = await RegisterAndLogin(NewUser(), admin: true);
        (await client.GetAsync(ApiRoutes.Admin.Ping)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task new_registration_is_assigned_the_member_role()
    {
        var creds = NewUser();
        await RealAuth.CreateClient().PostAsJsonAsync(ApiRoutes.Auth.Register, creds);

        using var scope = RealAuth.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await users.FindByEmailAsync(creds.Email);

        (await users.IsInRoleAsync(user!, Roles.Member)).ShouldBeTrue();
        (await users.IsInRoleAsync(user!, Roles.Admin)).ShouldBeFalse();
    }
}
