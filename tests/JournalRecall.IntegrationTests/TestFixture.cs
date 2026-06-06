using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Trace;
using HeimGuard;
using NSubstitute;
using JournalRecall.Api.Domain.Sessions.Ai;
using JournalRecall.Api.Domain.Summaries.Ai;
using JournalRecall.SharedTestHelpers.Fakes.Ai;
using JournalRecall.SharedTestHelpers.Utilities;

namespace JournalRecall.IntegrationTests;

/// <summary>
/// The integration collection fixture (PRD-0003, ADR-0006): boots the real <c>Program</c> once against a
/// single shared SQLite file, so the app's startup migrations run end-to-end exactly once for the whole
/// assembly (not <c>EnsureCreated</c>). The PeakLims-style <c>ConfigureServices</c> hook swaps a mocked
/// <see cref="IHttpContextAccessor"/> (singleton, so the current User can be set from any scope), a mocked
/// <see cref="IHeimGuardClient"/>, and the keyed AI <see cref="IChatClient"/>s for scriptable fakes.
/// Isolation between tests comes from a fresh User per <see cref="TestingServiceScope"/>, not a DB reset.
/// </summary>
public sealed class TestFixture : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"journalrecall-itest-{Guid.NewGuid():N}.db");
    private IntegrationTestWebApplicationFactory _factory = null!;

    /// <summary>The scope factory off the booted host — every <see cref="TestingServiceScope"/> uses it.</summary>
    public static IServiceScopeFactory ScopeFactory { get; private set; } = null!;

    /// <summary>The scriptable Cleanup model swapped in for the keyed "cleanup" client.</summary>
    public static ScriptableChatClient CleanupChat { get; private set; } = null!;

    /// <summary>The scriptable Summary model swapped in for the keyed "summary" client.</summary>
    public static ScriptableSummaryChatClient SummaryChat { get; private set; } = null!;

    /// <summary>The controllable clock the audit interceptor (and anything time-dependent) stamps from.</summary>
    public static TestTimeProvider Clock { get; private set; } = null!;

    /// <summary>Spans recorded by the in-memory exporter on the real tracing pipeline (AI-lifecycle spans).</summary>
    public static List<Activity> ExportedActivities { get; private set; } = null!;

    public Task InitializeAsync()
    {
        _factory = new IntegrationTestWebApplicationFactory(_dbPath);
        // Touching Services boots and starts the host, which runs the MigrationHostedService (migrations
        // once) and the role seeder before any test executes.
        ScopeFactory = _factory.Services.GetRequiredService<IServiceScopeFactory>();
        CleanupChat = _factory.CleanupChat;
        SummaryChat = _factory.SummaryChat;
        Clock = _factory.Clock;
        ExportedActivities = _factory.ExportedActivities;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        foreach (var path in new[] { _dbPath, $"{_dbPath}-shm", $"{_dbPath}-wal" })
            if (File.Exists(path)) File.Delete(path);
    }
}

/// <summary>The single serial collection every integration test joins (shared SQLite file + host).</summary>
[CollectionDefinition(nameof(TestFixture))]
public sealed class TestFixtureCollection : ICollectionFixture<TestFixture>;

/// <summary>
/// Boots the real <c>Program</c> against the shared SQLite file and applies the integration-only service
/// swaps. Faithful to production wiring; only the seams the PRD calls out are replaced.
/// </summary>
internal sealed class IntegrationTestWebApplicationFactory(string dbPath) : WebApplicationFactory<Program>
{
    public ScriptableChatClient CleanupChat { get; } = new();
    public ScriptableSummaryChatClient SummaryChat { get; } = new();
    public TestTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    public List<Activity> ExportedActivities { get; } = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:JournalRecall"] = $"Data Source={dbPath}",
            }));
        builder.ConfigureServices(services =>
        {
            // Current User comes from a ClaimsPrincipal on a mocked accessor — singleton so a scope can
            // set it and the (construction-time) DbContext tenant binding reads it (ADR-0006).
            services.RemoveAll<IHttpContextAccessor>();
            services.AddSingleton(_ => Substitute.For<IHttpContextAccessor>());

            // HeimGuard permitted-by-default; SetUserNotPermitted is the escape hatch.
            services.RemoveAll<IHeimGuardClient>();
            services.AddSingleton(_ => Substitute.For<IHeimGuardClient>());

            // A controllable clock so audit timestamps are deterministic (wins over TimeProvider.System).
            services.AddSingleton<TimeProvider>(Clock);

            // Capture AI-lifecycle spans off the real tracing pipeline so observability can be asserted.
            services.AddOpenTelemetry().WithTracing(t => t.AddInMemoryExporter(ExportedActivities));

            // Deterministic AI: scripted fakes win over the app's lazy keyed registrations.
            services.AddKeyedSingleton<IChatClient>(CleanupAgent.ModelKey, CleanupChat);
            services.AddKeyedSingleton<IChatClient>(SummaryAgent.ModelKey, SummaryChat);
        });
    }
}
