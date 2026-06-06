using System.Net;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Health;

/// <summary>
/// DI-wiring / walking-skeleton smoke test: booting the full host (Serilog, OpenTelemetry, EF, the
/// startup migration) and hitting <c>/api/health</c> exercises the whole composition root. A 200 here
/// proves the container resolves every registered service and the SQLite database was created and migrated.
/// </summary>
public class health_endpoint_tests(WebTestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task get_api_health_returns_200_and_the_sqlite_database_exists()
    {
        var client = RealAuth.CreateClient();

        var response = await client.GetAsync(ApiRoutes.Health);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        File.Exists(RealAuth.DbPath).ShouldBeTrue(); // startup migration created the .db file
    }
}
