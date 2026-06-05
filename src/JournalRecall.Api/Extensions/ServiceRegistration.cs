using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Serilog;
using JournalRecall.AI.DependencyInjection;
using JournalRecall.AI.OpenAI;
using JournalRecall.Api.Auth;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions.Services;

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

        // Identity + first-party JWT (cookie or bearer) authentication (ADR-0002).
        services.AddJournalRecallAuth(builder.Configuration);

        // Application stack: MediatR vertical slices + Mapster mappings (scan this assembly).
        var assembly = typeof(ServiceRegistration).Assembly;
        TypeAdapterConfig.GlobalSettings.Scan(assembly);
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));

        // AI agent runner (ADR-0004) + the BYO OpenAI-compatible "cleanup" model the Cleanup agent
        // resolves by logical name. The client is built lazily on first use, so a missing/blank
        // ChatModels:cleanup section doesn't fail startup — only an actual run would (Admin configures it).
        services.AddJournalRecallAgents()
            .AddChatModel(JournalRecall.Api.Domain.Sessions.Ai.CleanupAgent.ModelKey,
                builder.Configuration.GetSection("ChatModels:cleanup"));
        services.AddScoped<SessionCleanupRunner>();
    }
}
