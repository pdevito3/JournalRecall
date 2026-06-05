using Microsoft.Extensions.DependencyInjection;
using JournalRecall.AI.Observability;

namespace JournalRecall.AI.DependencyInjection;

/// <summary>Configures the telemetry content policy (ADR-0005).</summary>
public static class TelemetryConfigurationExtensions
{
    /// <summary>
    /// Configures telemetry. Example:
    /// <c>.Telemetry(t => { t.CaptureContent = env.IsDevelopment(); t.Redactor = new HealthPiiRedactor(); })</c>.
    /// </summary>
    public static IJournalRecallAgentsBuilder Telemetry(
        this IJournalRecallAgentsBuilder builder, Action<TelemetryOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        builder.Services.Configure(configure);
        return builder;
    }
}
