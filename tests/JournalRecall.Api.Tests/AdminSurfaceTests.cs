using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using JournalRecall.AI.OpenAI;
using JournalRecall.Api.Domain.Admin.Services;
using JournalRecall.Api.Domain.Identity;

namespace JournalRecall.Api.Tests;

/// <summary>
/// Admin surface (issue 0016): an Admin manages users (create / role / disable) and the app-wide AI
/// provider; a Member is forbidden (403); disabling a user prevents login; a configured provider takes
/// effect for the AI features; and the surface exposes no journal data (Privacy invariant).
/// </summary>
public class AdminSurfaceTests : IClassFixture<SkeletonWebApplicationFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SkeletonWebApplicationFactory _factory;

    public AdminSurfaceTests(SkeletonWebApplicationFactory factory) => _factory = factory;

    private sealed record Credentials(string Email, string Password);
    private sealed record AdminUserDto(Guid Id, string Email, List<string> Roles, bool IsDisabled);
    private sealed record AiProviderDto(string Provider, string? Endpoint, string Model, bool HasApiKey);

    private static Credentials NewUser() => new($"user-{Guid.NewGuid():N}@example.com", "Passw0rd!23");

    private async Task<HttpClient> RegisterAndLogin(Credentials creds, bool admin)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", creds);
        if (admin)
        {
            using var scope = _factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = await users.FindByEmailAsync(creds.Email);
            await users.AddToRoleAsync(user!, Roles.Admin);
        }
        await client.PostAsJsonAsync("/api/auth/login", creds);
        return client;
    }

    private Task<HttpClient> Admin() => RegisterAndLogin(NewUser(), admin: true);
    private Task<HttpClient> Member() => RegisterAndLogin(NewUser(), admin: false);

    private async Task<List<AdminUserDto>> ListUsers(HttpClient admin) =>
        (await admin.GetFromJsonAsync<List<AdminUserDto>>("/api/admin/users", Json))!;

    [Fact]
    public async Task A_member_is_forbidden_from_every_user_management_endpoint()
    {
        var member = await Member();
        var someId = Guid.NewGuid();

        (await member.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await member.PostAsJsonAsync("/api/admin/users", new { email = "x@y.z", password = "Passw0rd!23", role = "Member" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await member.PutAsJsonAsync($"/api/admin/users/{someId}/role", new { role = "Admin" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await member.PostAsync($"/api/admin/users/{someId}/disable", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await member.PutAsJsonAsync("/api/admin/ai-provider", new { provider = "OpenAI", model = "x" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_admin_can_create_a_user_change_its_role_and_disable_it()
    {
        var admin = await Admin();
        var creds = NewUser();

        var created = await admin.PostAsJsonAsync("/api/admin/users",
            new { email = creds.Email, password = creds.Password, role = "Member" });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var dto = (await created.Content.ReadFromJsonAsync<AdminUserDto>(Json))!;
        dto.Roles.ShouldBe(["Member"]);

        (await admin.PutAsJsonAsync($"/api/admin/users/{dto.Id}/role", new { role = "Admin" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await admin.PostAsync($"/api/admin/users/{dto.Id}/disable", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var after = (await ListUsers(admin)).Single(u => u.Id == dto.Id);
        after.Roles.ShouldBe(["Admin"]);
        after.IsDisabled.ShouldBeTrue();
    }

    [Fact]
    public async Task Disabling_a_user_prevents_their_login()
    {
        var creds = NewUser();
        await RegisterAndLogin(creds, admin: false); // the soon-to-be-disabled member
        var admin = await Admin();

        var target = (await ListUsers(admin)).Single(u => u.Email == creds.Email);
        (await admin.PostAsync($"/api/admin/users/{target.Id}/disable", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // A fresh login attempt with the right password is now rejected.
        var attempt = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", creds);
        attempt.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_admin_can_configure_the_app_wide_ai_provider_and_it_takes_effect()
    {
        var admin = await Admin();

        var put = await admin.PutAsJsonAsync("/api/admin/ai-provider", new
        {
            provider = "OpenAI",
            endpoint = "http://localhost:11434/v1",
            apiKey = "super-secret-key",
            model = "llama3.1",
        });
        put.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var dto = (await admin.GetFromJsonAsync<AiProviderDto>("/api/admin/ai-provider", Json))!;
        dto.Provider.ShouldBe("OpenAI");
        dto.Endpoint.ShouldBe("http://localhost:11434/v1");
        dto.Model.ShouldBe("llama3.1");
        dto.HasApiKey.ShouldBeTrue(); // the key is never echoed back, only its presence

        // The same resolver the Cleanup/Summary clients use now reflects the configured provider.
        using var scope = _factory.Services.CreateScope();
        var effective = scope.ServiceProvider.GetRequiredService<EffectiveChatModelOptions>();
        var resolved = await effective.ResolveAsync(new ChatModelOptions { Model = "fallback-model" });
        resolved.Endpoint.ShouldBe("http://localhost:11434/v1");
        resolved.Model.ShouldBe("llama3.1");
        resolved.ApiKey.ShouldBe("super-secret-key");
    }

    [Fact]
    public async Task The_admin_surface_exposes_no_journal_data()
    {
        // A member writes something private into their journal.
        var memberCreds = NewUser();
        var member = await RegisterAndLogin(memberCreds, admin: false);
        var created = await member.PostAsync("/api/sessions", null);
        var sessionId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        await member.PutAsJsonAsync($"/api/sessions/{sessionId}/draft", new { rawText = "SECRET-DIARY-CONTENT" });

        var admin = await Admin();

        // The user listing carries identity only — never journal text.
        var body = await (await admin.GetAsync("/api/admin/users")).Content.ReadAsStringAsync();
        body.ShouldContain(memberCreds.Email);
        body.ShouldNotContain("SECRET-DIARY-CONTENT");

        // There is deliberately no journal-reading admin endpoint.
        (await admin.GetAsync("/api/admin/sessions")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await admin.GetAsync($"/api/admin/users/{sessionId}/sessions")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
