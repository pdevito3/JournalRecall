using System.Net;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Sessions;

/// <summary>
/// The metadata endpoint's HTTP status mapping (issue 0011): a valid set is 204, an unknown mood is 400,
/// and another User's Session is 404. The behavior is covered at the integration layer; this asserts the
/// status contract.
/// </summary>
public class metadata_endpoint_tests(WebTestFixture fixture) : TestBase(fixture)
{
    private async Task<(HttpClient Client, Guid Id)> NewSession()
    {
        var client = await RealAuth.CreateAuthenticatedClientAsync();
        var created = await client.PostAsync(ApiRoutes.Sessions.Create(), null);
        return (client, (await created.ReadJsonAsync<SessionDto>())!.Id);
    }

    [Fact]
    public async Task a_valid_metadata_set_is_no_content()
    {
        var (client, id) = await NewSession();

        var response = await client.PutJsonAsync(ApiRoutes.Sessions.Metadata(id),
            new { topics = new[] { "work" }, people = Array.Empty<string>(), mood = (object?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task an_unknown_mood_is_unprocessable()
    {
        var (client, id) = await NewSession();

        var response = await client.PutJsonAsync(ApiRoutes.Sessions.Metadata(id),
            new { topics = Array.Empty<string>(), people = Array.Empty<string>(), mood = new { key = "Ecstatic", customValue = (string?)null } });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task another_users_session_is_not_found()
    {
        var (_, id) = await NewSession();
        var bob = await RealAuth.CreateAuthenticatedClientAsync();

        var response = await bob.PutJsonAsync(ApiRoutes.Sessions.Metadata(id),
            new { topics = new[] { "x" }, people = Array.Empty<string>(), mood = (object?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
