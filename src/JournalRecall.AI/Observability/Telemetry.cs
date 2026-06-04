using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace JournalRecall.AI.Observability;

/// <summary>
/// The single, stable identity for AI-lifecycle telemetry. The host registers these names with
/// OpenTelemetry in Phase 0 so later slices (Cleanup spans, token metrics — issue 0017) only have to
/// emit against <see cref="ActivitySource"/> / <see cref="Meter"/>; nothing in the host wiring changes.
/// </summary>
public static class Telemetry
{
    public const string SourceName = "JournalRecall.AI";
    public const string MeterName = "JournalRecall.AI";

    public static readonly ActivitySource ActivitySource = new(SourceName);
    public static readonly Meter Meter = new(MeterName);
}
