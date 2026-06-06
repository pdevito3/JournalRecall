using System.Net;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Sessions;

/// <summary>
/// Functional reference test (PRD-0003, TEST-0004): create + read a Session over the <b>real</b> auth
/// pipeline — register→login, the real cookie + X-CSRF header — asserting HTTP status and response shape.
/// </summary>
public class create_session_over_real_auth_tests(WebTestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task create_then_get_round_trips_over_real_auth()
    {
        var client = await RealAuth.CreateAuthenticatedClientAsync();

        var created = await client.PostAsync(ApiRoutes.Sessions.Create(), null);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var dto = await created.ReadJsonAsync<SessionDto>();
        dto.ShouldNotBeNull();

        var reread = await client.GetAsync(ApiRoutes.Sessions.Get(dto!.Id));
        reread.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await reread.ReadJsonAsync<SessionDto>())!.Id.ShouldBe(dto.Id);
    }

    [Fact]
    public async Task anonymous_callers_cannot_create_sessions()
    {
        var anon = RealAuth.CreateClient();

        var created = await anon.PostAsync(ApiRoutes.Sessions.Create(), null);

        created.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
