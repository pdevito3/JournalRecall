using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions;

namespace JournalRecall.Api.Tests;

/// <summary>A clock the test controls so audit timestamps are deterministic and advanceable.</summary>
internal sealed class TestTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>Boots the real app with a controllable <see cref="TimeProvider"/> so the audit clock is deterministic.</summary>
public sealed class AuditWebApplicationFactory : SkeletonWebApplicationFactory
{
    internal TestTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    protected override void ConfigureTestServices(IServiceCollection services) =>
        services.AddSingleton<TimeProvider>(Clock); // registered after the app's TimeProvider.System → wins
}

/// <summary>
/// Audit fields (BaseEntity): the DbContext stamps CreatedAt/CreatedBy on insert and UpdatedAt/UpdatedBy
/// on every save, from the request's clock and current user — without the domain setting them.
/// </summary>
public class AuditFieldsTests : IClassFixture<AuditWebApplicationFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly AuditWebApplicationFactory _factory;

    public AuditFieldsTests(AuditWebApplicationFactory factory) => _factory = factory;

    private sealed record Credentials(string Email, string Password);
    private sealed record SessionDto(Guid Id);

    private async Task<HttpClient> SignedInClient()
    {
        var client = _factory.CreateClient();
        var creds = new Credentials($"user-{Guid.NewGuid():N}@example.com", "Passw0rd!23");
        await client.PostAsJsonAsync("/api/auth/register", creds);
        await client.PostAsJsonAsync("/api/auth/login", creds);
        return client;
    }

    private async Task<Session> LoadSession(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JournalRecallDbContext>();
        return await db.Sessions.IgnoreQueryFilters().AsNoTracking().FirstAsync(s => s.Id == id);
    }

    [Fact]
    public async Task Insert_stamps_creation_and_modification_from_the_clock_and_current_user()
    {
        var start = _factory.Clock.GetUtcNow();
        var client = await SignedInClient();

        var created = await client.PostAsync("/api/sessions", null);
        var id = (await created.Content.ReadFromJsonAsync<SessionDto>(Json))!.Id;

        var session = await LoadSession(id);
        session.CreatedAt.ShouldBe(start);
        session.UpdatedAt.ShouldBe(start);
        session.CreatedBy.ShouldBe(session.UserId);   // the creator stamped as the author
        session.UpdatedBy.ShouldBe(session.UserId);
    }

    [Fact]
    public async Task Update_advances_only_the_modification_fields()
    {
        var client = await SignedInClient();
        var created = await client.PostAsync("/api/sessions", null);
        var id = (await created.Content.ReadFromJsonAsync<SessionDto>(Json))!.Id;
        var atCreate = await LoadSession(id);

        _factory.Clock.Advance(TimeSpan.FromMinutes(5));
        (await client.PutAsJsonAsync($"/api/sessions/{id}/draft", new { rawText = "a later edit" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterEdit = await LoadSession(id);
        afterEdit.CreatedAt.ShouldBe(atCreate.CreatedAt);                       // creation is immutable
        afterEdit.CreatedBy.ShouldBe(atCreate.CreatedBy);
        afterEdit.UpdatedAt.ShouldBe(atCreate.CreatedAt.AddMinutes(5));         // modification advanced
        afterEdit.UpdatedBy.ShouldBe(afterEdit.UserId);
    }
}
