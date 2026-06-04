using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Serilog;
using JournalRecall.Api.Databases;

namespace JournalRecall.Api.Extensions;

public static class ServiceRegistration
{
    public static void ConfigureServices(this WebApplicationBuilder builder)
    {
        var services = builder.Services;

        // Structured logging via Serilog as the sole provider (no duplicate default-console output).
        // Console for humans + OTLP to the Aspire dashboard when an endpoint is configured. Levels and
        // per-namespace overrides (wildcarding "JournalRecall") come from configuration.
        builder.Logging.ClearProviders();
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        services.AddSerilog((sp, lc) =>
        {
            lc.ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(sp)
                .Enrich.FromLogContext()
                .WriteTo.Console();

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                lc.WriteTo.OpenTelemetry(otlp =>
                {
                    otlp.Endpoint = otlpEndpoint;
                    otlp.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = builder.Environment.ApplicationName,
                    };
                });
        });

        services.AddDbContext<JournalRecallDbContext>(options =>
            options.UseSqlite(builder.Configuration.GetConnectionString("JournalRecall"))
                // Demote command-error logging to Debug: the only one in practice is the benign
                // first-run "__EFMigrationsHistory does not exist" probe inside MigrateAsync. Real
                // failures still surface as thrown exceptions.
                .ConfigureWarnings(w => w.Log((RelationalEventId.CommandError, Microsoft.Extensions.Logging.LogLevel.Debug))));

        // Apply migrations at startup so the SQLite file + schema exist on first run.
        services.AddHostedService<MigrationHostedService<JournalRecallDbContext>>();
    }
}
