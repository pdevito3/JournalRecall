using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using JournalRecall.Api.Domain.Identity;

namespace JournalRecall.Api.Tests;

/// <summary>
/// First-run setup &amp; root Admin (issue 0021, PRD-0001). Each test boots its own factory so the
/// instance starts with zero Users (the precondition for setup). Covers first-setup-creates-Admin,
/// second-setup-409, the concurrent single-Admin outcome, and the length-10/no-composition policy.
/// </summary>
public class SetupTests
{
    private sealed record Credentials(string Email, string Password);
    private static Credentials NewUser() => new($"user-{Guid.NewGuid():N}@example.com", "Passw0rd!23");

    [Fact]
    public async Task The_first_setup_creates_a_root_admin_with_the_operator_supplied_password()
    {
        using var factory = new SkeletonWebApplicationFactory();
        var creds = NewUser();

        (await factory.CreateClient().PostAsJsonAsync("/api/setup", creds)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await users.FindByEmailAsync(creds.Email);
        (await users.IsInRoleAsync(user!, Roles.Admin)).ShouldBeTrue();
        (await users.CheckPasswordAsync(user!, creds.Password)).ShouldBeTrue();
    }

    [Fact]
    public async Task A_second_setup_returns_409_once_a_user_exists()
    {
        using var factory = new SkeletonWebApplicationFactory();
        var client = factory.CreateClient();

        (await client.PostAsJsonAsync("/api/setup", NewUser())).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/setup", NewUser())).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Concurrent_setups_resolve_to_exactly_one_root_admin()
    {
        using var factory = new SkeletonWebApplicationFactory();
        var client = factory.CreateClient();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => client.PostAsJsonAsync("/api/setup", NewUser())));

        results.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        results.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(7);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        (await users.Users.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task The_password_policy_enforces_length_10_without_composition()
    {
        using var factory = new SkeletonWebApplicationFactory();
        var client = factory.CreateClient();

        // 9 chars → rejected for length.
        (await client.PostAsJsonAsync("/api/setup", new { email = "op@example.com", password = "short1234" }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        // 10 chars, no digit/upper/symbol → accepted (composition is not required).
        (await client.PostAsJsonAsync("/api/setup", new { email = "op@example.com", password = "abcdefghij" }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
