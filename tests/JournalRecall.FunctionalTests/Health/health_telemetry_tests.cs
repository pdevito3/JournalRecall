using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Health;

/// <summary>
/// Baseline observability: a request to <c>/api/health</c> produces an OpenTelemetry trace. The factory
/// wires an in-memory exporter onto the real tracing pipeline, so a recorded span here means the ASP.NET
/// Core instrumentation is active and <c>/api/health</c> is not filtered out.
/// </summary>
public class health_telemetry_tests(WebTestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task get_api_health_produces_an_opentelemetry_span()
    {
        var client = RealAuth.CreateClient();
        RealAuth.ExportedActivities.Clear();

        await client.GetAsync(ApiRoutes.Health);

        RealAuth.ExportedActivities.ShouldContain(a =>
            (a.GetTagItem("url.path") as string) == "/api/health" ||
            (a.GetTagItem("http.route") as string) == "/api/health" ||
            a.DisplayName.Contains("/api/health"));
    }
}
