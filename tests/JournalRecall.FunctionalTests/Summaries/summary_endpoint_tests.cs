using System.Net;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Summaries;

/// <summary>
/// The Summaries endpoint's HTTP contract (issue 0013): an unknown period or a malformed date is rejected
/// with 400 on both the read and the generate routes. Generation/roll-up/staleness behavior is covered at
/// the integration layer.
/// </summary>
public class summary_endpoint_tests(WebTestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task an_unknown_period_is_bad_request()
    {
        var client = await RealAuth.CreateAuthenticatedClientAsync();

        (await client.GetAsync($"{ApiRoutes.Base}/summaries/decade/2026-08-01"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostAsync($"{ApiRoutes.Base}/summaries/decade/2026-08-01/generate", null))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task a_malformed_date_is_bad_request()
    {
        var client = await RealAuth.CreateAuthenticatedClientAsync();

        (await client.GetAsync($"{ApiRoutes.Base}/summaries/day/notadate"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
