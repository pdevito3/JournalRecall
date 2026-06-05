using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

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
            ConfigureTestServices(services);
        });
    }

    /// <summary>Hook for subclasses to override host services (e.g. stub the AI chat client).</summary>
    protected virtual void ConfigureTestServices(IServiceCollection services) { }

    public string DbPath => _dbPath;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            foreach (var path in new[] { _dbPath, $"{_dbPath}-shm", $"{_dbPath}-wal" })
                if (File.Exists(path)) File.Delete(path);
    }
}
