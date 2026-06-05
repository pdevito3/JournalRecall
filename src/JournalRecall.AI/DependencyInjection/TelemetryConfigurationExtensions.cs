using Microsoft.Extensions.Configuration;
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

    /// <summary>
    /// Binds the content policy from a configuration section (per environment), keeping
    /// <see cref="TelemetryOptions.CaptureContent"/> default-off when the section is absent. The
    /// <see cref="TelemetryOptions.Redactor"/> is code-supplied (not bindable) — pass <paramref name="configure"/>
    /// to set it. Bind runs first, so an explicit override always wins.
    /// </summary>
    public static IJournalRecallAgentsBuilder Telemetry(
        this IJournalRecallAgentsBuilder builder, IConfiguration section, Action<TelemetryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(section);
        builder.Services.Configure<TelemetryOptions>(o => o.CaptureContent = section.GetValue("CaptureContent", false));
        if (configure is not null)
            builder.Services.Configure(configure);
        return builder;
    }
}
