using Shouldly;

namespace JournalRecall.Api.Tests;

/// <summary>
/// Proves the baseline observability acceptance criterion: a request to <c>/api/health</c> produces
/// an OpenTelemetry trace. The factory wires an in-memory exporter onto the real tracing pipeline, so
/// a recorded span here means the ASP.NET Core instrumentation is active and <c>/api/health</c> is not
/// filtered out — the same pipeline a later slice's AI-lifecycle spans extend.
/// </summary>
public class HealthTelemetryTests : IClassFixture<SkeletonWebApplicationFactory>
{
    private readonly SkeletonWebApplicationFactory _factory;

    public HealthTelemetryTests(SkeletonWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GET_api_health_produces_an_opentelemetry_span()
    {
        var client = _factory.CreateClient();
        _factory.ExportedActivities.Clear();

        await client.GetAsync("/api/health");

        _factory.ExportedActivities.ShouldContain(a =>
            (a.GetTagItem("url.path") as string) == "/api/health" ||
            (a.GetTagItem("http.route") as string) == "/api/health" ||
            a.DisplayName.Contains("/api/health"));
    }
}
