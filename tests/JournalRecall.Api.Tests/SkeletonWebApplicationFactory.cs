using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using JournalRecall.Api.Auth;

namespace JournalRecall.Api.Tests;

/// <summary>
/// Boots the real Program against a throwaway SQLite file so startup migrations run end-to-end
/// without touching the developer's journalrecall.db. The file is removed on dispose. An in-memory
/// trace exporter is wired in so tests can assert the OpenTelemetry pipeline actually produces spans.
/// </summary>
public class SkeletonWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"journalrecall-test-{Guid.NewGuid():N}.db");

    public List<Activity> ExportedActivities { get; } = [];

    public SkeletonWebApplicationFactory()
    {
        // Cookie-prefix hardening (issue 0020) makes the auth cookies Secure unconditionally, and the
        // in-memory CookieContainer only *sends* Secure cookies over https — so address the TestServer
        // over https (it fakes TLS and sets Request.IsHttps from the URI scheme).
        ClientOptions.BaseAddress = new Uri("https://localhost");
    }

    /// <summary>Every test client carries the X-CSRF header so the CSRF middleware (issue 0020) admits its
    /// mutating requests — mirroring the SPA, which sends it on every mutation.</summary>
    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Add(JournalRecall.Api.Auth.CsrfMiddleware.HeaderName, "1");
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
            services.AddOpenTelemetry().WithTracing(tracing =>
                tracing.AddInMemoryExporter(ExportedActivities));
            // Most tests just need to create Users via /api/auth/register, which is closed by default
            // (issue 0023). Open it for the test instance so those tests need no policy ceremony;
            // policy-specific tests use a factory that keeps the real closed default (see below).
            if (SeedSelfRegistration)
                services.AddHostedService<SelfRegistrationSeeder>();
            ConfigureTestServices(services);
        });
    }

    /// <summary>Whether to seed the test instance with self-registration enabled (default true). Override
    /// to false to exercise the real closed-by-default behavior.</summary>
    protected virtual bool SeedSelfRegistration => true;

    /// <summary>Hook for subclasses to override host services (e.g. stub the AI chat client).</summary>
    protected virtual void ConfigureTestServices(IServiceCollection services) { }

    public string DbPath => _dbPath;

    /// <summary>Enables self-registration after migrations so register-based test setup works. Registered
    /// last (test ConfigureServices runs after the app's), so it starts after the migration hosted service.</summary>
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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            foreach (var path in new[] { _dbPath, $"{_dbPath}-shm", $"{_dbPath}-wal" })
                if (File.Exists(path)) File.Delete(path);
    }
}

/// <summary>A factory that leaves self-registration at its real default (closed) so registration-policy
/// and onboarding tests exercise the production default (issue 0023).</summary>
public sealed class ClosedRegistrationWebApplicationFactory : SkeletonWebApplicationFactory
{
    protected override bool SeedSelfRegistration => false;
}
