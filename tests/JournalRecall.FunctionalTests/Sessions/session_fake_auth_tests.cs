using System.Net;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Sessions;

/// <summary>
/// Functional reference test (PRD-0003, TEST-0004): the opt-in <b>fake auth</b> path — a caller who needs
/// to be someone to reach an endpoint but isn't testing auth. It skips only token issuance; the request
/// still flows through CSRF and the access gate.
/// </summary>
public class session_fake_auth_tests(WebTestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task a_fake_auth_user_can_create_and_get_their_session()
    {
        var userId = Guid.CreateVersion7();
        var client = FakeAuth.CreateClient().AsUser(userId);

        var created = await client.PostAsync(ApiRoutes.Sessions.Create(), null);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var dto = await created.ReadJsonAsync<SessionDto>();

        var get = await client.GetAsync(ApiRoutes.Sessions.Get(dto!.Id));
        get.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task without_fake_auth_headers_the_caller_is_anonymous()
    {
        var anon = FakeAuth.CreateClient();

        var created = await anon.PostAsync(ApiRoutes.Sessions.Create(), null);

        created.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
