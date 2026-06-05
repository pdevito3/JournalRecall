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

        // The clock the audit interceptor stamps from (overridable in tests).
        services.AddSingleton(TimeProvider.System);

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

        // AI agent runner (ADR-0004). The Cleanup/Summary models resolve through a ConfigurableChatClient
        // that reads the effective provider at call time — the Admin-configured app-wide provider when set
        // (issue 0016), else the appsettings ChatModels:* fallback the app booted with. So an Admin change
        // takes effect on the next run without a restart, and a missing config never fails startup.
        // Telemetry content policy (issue 0017): metadata is always captured; prompt/response content
        // capture is opt-in per environment via the "Telemetry" section — default-off for this
        // intimate-journal domain — and anything captured passes through the redaction hook before
        // export. Provide a real redactor here when capture is enabled.
        services.AddJournalRecallAgents()
            .Telemetry(builder.Configuration.GetSection("Telemetry"));
        services.AddScoped<JournalRecall.Api.Domain.Admin.Services.EffectiveChatModelOptions>();
        AddConfigurableChatModel(services, JournalRecall.Api.Domain.Sessions.Ai.CleanupAgent.ModelKey,
            builder.Configuration.GetSection("ChatModels:cleanup"));
        AddConfigurableChatModel(services, JournalRecall.Api.Domain.Summaries.Ai.SummaryAgent.ModelKey,
            builder.Configuration.GetSection("ChatModels:summary"));
        services.AddScoped<SessionCleanupRunner>();
        services.AddScoped<JournalRecall.Api.Domain.Summaries.Services.SummarySourceReader>();
        services.AddScoped<JournalRecall.Api.Domain.Summaries.Services.SummaryRollupReader>();
        services.AddScoped<JournalRecall.Api.Domain.Summaries.Services.SummaryStaleness>();
        services.AddScoped<JournalRecall.Api.Domain.Summaries.Services.SummaryGenerator>();
    }

    /// <summary>
    /// Registers a logical chat model as a <see cref="JournalRecall.Api.Domain.Admin.Services.ConfigurableChatClient"/>
    /// keyed by name, with the appsettings section as its boot-time fallback. A test (or a future override)
    /// can register a keyed <c>IChatClient</c> for the same key afterwards and win.
    /// </summary>
    private static void AddConfigurableChatModel(IServiceCollection services, string key, IConfiguration section)
    {
        var fallback = new JournalRecall.AI.OpenAI.ChatModelOptions();
        section.Bind(fallback);
        services.AddKeyedSingleton<Microsoft.Extensions.AI.IChatClient>(key, (sp, _) =>
            new JournalRecall.Api.Domain.Admin.Services.ConfigurableChatClient(
                sp.GetRequiredService<IServiceScopeFactory>(), fallback));
    }
}
