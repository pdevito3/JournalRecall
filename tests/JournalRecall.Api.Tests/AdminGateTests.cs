using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using JournalRecall.Api.Domain.Identity;

namespace JournalRecall.Api.Tests;

/// <summary>
/// Roles + admin gate (issue 0003): the admin endpoint requires the CanAccessAdmin permission, which
/// only the Admin role grants. New registrations are Member by default.
/// </summary>
public class AdminGateTests : IClassFixture<SkeletonWebApplicationFactory>
{
    private readonly SkeletonWebApplicationFactory _factory;

    public AdminGateTests(SkeletonWebApplicationFactory factory) => _factory = factory;

    private sealed record Credentials(string Email, string Password);
    private static Credentials NewUser() => new($"user-{Guid.NewGuid():N}@example.com", "Passw0rd!");

    [Fact]
    public async Task Anonymous_caller_gets_401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/admin/ping");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Member_gets_403()
    {
        var client = await RegisterAndLogin(NewUser(), promoteToAdmin: false);
        (await client.GetAsync("/api/admin/ping")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_gets_200()
    {
        var client = await RegisterAndLogin(NewUser(), promoteToAdmin: true);
        (await client.GetAsync("/api/admin/ping")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task New_registration_is_assigned_the_Member_role()
    {
        var creds = NewUser();
        await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", creds);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await users.FindByEmailAsync(creds.Email);

        (await users.IsInRoleAsync(user!, Roles.Member)).ShouldBeTrue();
        (await users.IsInRoleAsync(user!, Roles.Admin)).ShouldBeFalse();
    }

    private async Task<HttpClient> RegisterAndLogin(Credentials creds, bool promoteToAdmin)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", creds);

        if (promoteToAdmin)
        {
            using var scope = _factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = await users.FindByEmailAsync(creds.Email);
            await users.AddToRoleAsync(user!, Roles.Admin);
        }

        // Log in after any promotion so the minted JWT carries the right role claims.
        await client.PostAsJsonAsync("/api/auth/login", creds);
        return client;
    }
}
