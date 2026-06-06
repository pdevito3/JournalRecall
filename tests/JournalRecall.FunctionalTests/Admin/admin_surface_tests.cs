using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using JournalRecall.AI.OpenAI;
using JournalRecall.Api.Domain.Admin.Services;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.FunctionalTests.Auth;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Admin;

/// <summary>
/// Admin surface (issue 0016): an Admin manages users (create / role / disable) and the app-wide AI
/// provider; a Member is forbidden (403); disabling a user prevents login; a configured provider takes
/// effect for the AI features; and the surface exposes no journal data (Privacy invariant).
/// </summary>
public class admin_surface_tests(WebTestFixture fixture) : AuthTestBase(fixture)
{
    private sealed record AdminUserDto(Guid Id, string Username, List<string> Roles, bool IsDisabled);
    private sealed record AiProviderDto(string Provider, string? Endpoint, string Model, bool HasApiKey);

    private Task<List<AdminUserDto>> ListUsers(HttpClient admin) =>
        admin.GetFromJsonAsync<List<AdminUserDto>>(ApiRoutes.Admin.Users, HttpClientExtensions.Web)!;

    [Fact]
    public async Task a_member_is_forbidden_from_every_user_management_endpoint()
    {
        var member = await RegisterAndLogin(NewUser());
        var someId = Guid.NewGuid();

        (await member.GetAsync(ApiRoutes.Admin.Users)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await member.PostJsonAsync(ApiRoutes.Admin.Users, new { username = "someuser", password = Password, role = "Member" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await member.PutJsonAsync(ApiRoutes.Admin.Role(someId), new { role = "Admin" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await member.PostAsync(ApiRoutes.Admin.Disable(someId), null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await member.PutJsonAsync(ApiRoutes.Admin.AiProvider, new { provider = "OpenAI", model = "x" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task an_admin_can_create_a_user_change_its_role_and_disable_it()
    {
        var admin = await AdminClient();
        var creds = NewUser();

        var created = await admin.PostJsonAsync(ApiRoutes.Admin.Users,
            new { username = creds.Username, password = creds.Password, role = "Member" });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var dto = (await created.ReadJsonAsync<AdminUserDto>())!;
        dto.Roles.ShouldBe(["Member"]);

        (await admin.PutJsonAsync(ApiRoutes.Admin.Role(dto.Id), new { role = "Admin" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await admin.PostAsync(ApiRoutes.Admin.Disable(dto.Id), null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var after = (await ListUsers(admin)).Single(u => u.Id == dto.Id);
        after.Roles.ShouldBe(["Admin"]);
        after.IsDisabled.ShouldBeTrue();
    }

    [Fact]
    public async Task disabling_a_user_prevents_their_login()
    {
        var creds = NewUser();
        await RegisterAndLogin(creds); // the soon-to-be-disabled member
        var admin = await AdminClient();

        var target = (await ListUsers(admin)).Single(u => u.Username == creds.Username);
        (await admin.PostAsync(ApiRoutes.Admin.Disable(target.Id), null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var attempt = await RealAuth.CreateClient().PostAsJsonAsync(ApiRoutes.Auth.Login, creds);
        attempt.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task an_admin_can_configure_the_app_wide_ai_provider_and_it_takes_effect()
    {
        var admin = await AdminClient();

        var put = await admin.PutJsonAsync(ApiRoutes.Admin.AiProvider, new
        {
            provider = "OpenAI",
            endpoint = "http://localhost:11434/v1",
            apiKey = "super-secret-key",
            model = "llama3.1",
        });
        put.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var dto = (await admin.GetFromJsonAsync<AiProviderDto>(ApiRoutes.Admin.AiProvider, HttpClientExtensions.Web))!;
        dto.Provider.ShouldBe("OpenAI");
        dto.Endpoint.ShouldBe("http://localhost:11434/v1");
        dto.Model.ShouldBe("llama3.1");
        dto.HasApiKey.ShouldBeTrue(); // the key is never echoed back, only its presence

        // The same resolver the Cleanup/Summary clients use now reflects the configured provider.
        using var scope = RealAuth.Services.CreateScope();
        var effective = scope.ServiceProvider.GetRequiredService<EffectiveChatModelOptions>();
        var resolved = await effective.ResolveAsync(new ChatModelOptions { Model = "fallback-model" });
        resolved.Endpoint.ShouldBe("http://localhost:11434/v1");
        resolved.Model.ShouldBe("llama3.1");
        resolved.ApiKey.ShouldBe("super-secret-key");
    }

    [Fact]
    public async Task the_admin_surface_exposes_no_journal_data()
    {
        // A member writes something private into their journal.
        var memberCreds = NewUser();
        var member = await RegisterAndLogin(memberCreds);
        var created = await member.PostAsync(ApiRoutes.Sessions.Create(), null);
        var sessionId = (await created.ReadJsonAsync<SessionDto>())!.Id;
        await member.PutJsonAsync(ApiRoutes.Sessions.Draft(sessionId), new { rawText = "SECRET-DIARY-CONTENT" });

        var admin = await AdminClient();

        // The user listing carries identity only — never journal text.
        var body = await (await admin.GetAsync(ApiRoutes.Admin.Users)).Content.ReadAsStringAsync();
        body.ShouldContain(memberCreds.Username);
        body.ShouldNotContain("SECRET-DIARY-CONTENT");

        // There is deliberately no journal-reading admin endpoint.
        (await admin.GetAsync($"{ApiRoutes.Admin.Root}/sessions")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await admin.GetAsync($"{ApiRoutes.Admin.Users}/{sessionId}/sessions")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
