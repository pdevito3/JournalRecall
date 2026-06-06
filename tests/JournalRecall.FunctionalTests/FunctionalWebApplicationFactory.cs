using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using JournalRecall.Api.Auth;
using JournalRecall.Api.Domain.Sessions.Ai;
using JournalRecall.Api.Domain.Summaries.Ai;
using JournalRecall.SharedTestHelpers.Fakes.Ai;

namespace JournalRecall.FunctionalTests;

/// <summary>
/// Shared base for the functional factories (PRD-0003): boots the real <c>Program</c> against a throwaway
/// SQLite file (so startup migrations run end-to-end), addresses the host over https (the hardened cookies
/// are Secure — ADR-0005), sends the <c>X-CSRF</c> header on every client (mirroring the SPA), and opens
/// self-registration so the real register→login flow works. The file is removed on dispose.
/// </summary>
public abstract class FunctionalWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"journalrecall-ftest-{Guid.NewGuid():N}.db");

    /// <summary>The scriptable Cleanup model swapped in for the keyed "cleanup" client (deterministic AI).</summary>
    public ScriptableChatClient CleanupChat { get; } = new();

    /// <summary>The scriptable Summary model swapped in for the keyed "summary" client.</summary>
    public ScriptableSummaryChatClient SummaryChat { get; } = new();

    /// <summary>Spans recorded by the in-memory exporter wired onto the real tracing pipeline.</summary>
    public List<Activity> ExportedActivities { get; } = [];

    /// <summary>The throwaway SQLite file path (created by the startup migration).</summary>
    public string DbPath => _dbPath;

    protected FunctionalWebApplicationFactory() =>
        ClientOptions.BaseAddress = new Uri("https://localhost");

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Add(CsrfMiddleware.HeaderName, "1");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:JournalRecall"] = $"Data Source={_dbPath}",
            }));
        builder.ConfigureServices(services =>
        {
            // Open self-registration so register-based setup works (the real default is closed — issue 0023).
            // App-global tests that exercise the real closed default override this to false.
            if (SeedSelfRegistration)
                services.AddHostedService<SelfRegistrationSeeder>();
            // Deterministic AI: scripted fakes win over the app's lazy keyed registrations.
            services.AddKeyedSingleton<IChatClient>(CleanupAgent.ModelKey, CleanupChat);
            services.AddKeyedSingleton<IChatClient>(SummaryAgent.ModelKey, SummaryChat);
            // Capture spans off the real tracing pipeline so telemetry can be asserted.
            services.AddOpenTelemetry().WithTracing(t => t.AddInMemoryExporter(ExportedActivities));
            ConfigureTestServices(services);
        });
    }

    /// <summary>Hook for subclasses to add test-only services (e.g. the fake auth scheme).</summary>
    protected virtual void ConfigureTestServices(IServiceCollection services) { }

    /// <summary>Whether to open self-registration after migrations (default true). App-global tests override
    /// to false to exercise the real closed-by-default behavior (issue 0023).</summary>
    protected virtual bool SeedSelfRegistration => true;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            foreach (var path in new[] { _dbPath, $"{_dbPath}-shm", $"{_dbPath}-wal" })
                if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>Enables self-registration after migrations so register-based test setup works.</summary>
    private sealed class SelfRegistrationSeeder(IServiceScopeFactory scopeFactory) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AuthSettingsService>()
                .SetSelfRegistrationAsync(true, cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
