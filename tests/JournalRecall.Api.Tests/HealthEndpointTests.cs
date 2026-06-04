using System.Net;
using Shouldly;

namespace JournalRecall.Api.Tests;

/// <summary>
/// DI-wiring / walking-skeleton smoke test: booting the full host (Serilog, OpenTelemetry, EF, the
/// startup migration) and hitting <c>/api/health</c> exercises the whole composition root. A 200 here
/// proves the container resolves every registered service and the SQLite database was created and
/// migrated on first run.
/// </summary>
public class HealthEndpointTests : IClassFixture<SkeletonWebApplicationFactory>
{
    private readonly SkeletonWebApplicationFactory _factory;

    public HealthEndpointTests(SkeletonWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GET_api_health_returns_200_and_creates_the_sqlite_database()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        File.Exists(_factory.DbPath).ShouldBeTrue(); // startup migration created the .db file
    }
}
