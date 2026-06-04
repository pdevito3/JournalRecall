using System.Reflection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using JournalRecall.AI.Observability;

namespace JournalRecall.Api.Extensions;

/// <summary>
/// Common host wiring (formerly the Aspire ServiceDefaults project, inlined here): OpenTelemetry,
/// health checks, resilience, and service discovery. Kept in-app rather than a separate shared
/// project — opinions live in JournalRecall.Api.
/// </summary>
public static class HostExtensions
{
    private const string DefaultServiceName = "journalrecall-api";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // Service identity + resource attributes (Aspire sets OTEL_SERVICE_NAME; fall back to a default).
        var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? DefaultServiceName;
        var serviceVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion: serviceVersion, serviceInstanceId: Environment.MachineName)
                .AddAttributes([new("deployment.environment", builder.Environment.EnvironmentName)]))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(Telemetry.MeterName))         // AI run/turn/tool/token metrics (issue 0017)
            .WithTracing(tracing => tracing
                .AddSource(Telemetry.SourceName)         // AI-lifecycle spans (issue 0017)
                .AddAspNetCoreInstrumentation(o => o.RecordException = true)
                .AddHttpClientInstrumentation(o => o.RecordException = true));

        // Logs are exported by Serilog's OpenTelemetry sink (see ServiceRegistration); only traces +
        // metrics use the OTLP exporter here. Active only when an OTLP endpoint is configured (Aspire).
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            builder.Services.AddOpenTelemetry().UseOtlpExporter();

        return builder;
    }
}
