using Shouldly;
using JournalRecall.AI.Observability;

namespace JournalRecall.AI.Tests;

/// <summary>
/// Pure test (no IO, no DI): the AI telemetry identity is a stable contract the host's OpenTelemetry
/// wiring depends on. If these names change, traces/metrics silently stop flowing — pin them.
/// </summary>
public class TelemetryTests
{
    [Fact]
    public void ActivitySource_and_Meter_use_the_published_source_name()
    {
        Telemetry.SourceName.ShouldBe("JournalRecall.AI");
        Telemetry.ActivitySource.Name.ShouldBe(Telemetry.SourceName);
        Telemetry.Meter.Name.ShouldBe(Telemetry.MeterName);
    }
}
