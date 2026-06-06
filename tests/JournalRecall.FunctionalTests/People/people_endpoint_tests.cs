using System.Net;
using JournalRecall.Api.Domain.People.Dtos;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.People;

/// <summary>
/// The Person directory endpoints' HTTP contract (PRD-0006, RICH-005): create is 201, list returns the
/// caller's People, rename is 204 and reflected on the next read, an unknown id is 404, another User's
/// directory is invisible/unwritable, and the routes require auth. Driven over the fake-auth host.
/// </summary>
public class people_endpoint_tests(WebTestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task create_then_list_returns_the_person()
    {
        var client = FakeAuth.CreateClient().AsUser(Guid.CreateVersion7());

        var created = await client.PostJsonAsync(ApiRoutes.People.Create(), new { label = "Sam" });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var dto = await created.ReadJsonAsync<PersonDto>();
        dto!.Label.ShouldBe("Sam");

        var list = await (await client.GetAsync(ApiRoutes.People.Root)).ReadJsonAsync<List<PersonDto>>();
        list!.Select(p => p.Label).ShouldContain("Sam");
    }

    [Fact]
    public async Task rename_is_no_content_and_reflected_on_read()
    {
        var client = FakeAuth.CreateClient().AsUser(Guid.CreateVersion7());
        var dto = await (await client.PostJsonAsync(ApiRoutes.People.Create(), new { label = "Sam" }))
            .ReadJsonAsync<PersonDto>();

        var renamed = await client.PatchJsonAsync(ApiRoutes.People.Rename(dto!.Id), new { label = "Samuel" });
        renamed.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var list = await (await client.GetAsync(ApiRoutes.People.Root)).ReadJsonAsync<List<PersonDto>>();
        list!.Single(p => p.Id == dto.Id).Label.ShouldBe("Samuel");
    }

    [Fact]
    public async Task renaming_an_unknown_person_is_not_found()
    {
        var client = FakeAuth.CreateClient().AsUser(Guid.CreateVersion7());

        var response = await client.PatchJsonAsync(ApiRoutes.People.Rename(Guid.CreateVersion7()), new { label = "X" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task another_users_directory_is_invisible_and_unwritable()
    {
        var alice = FakeAuth.CreateClient().AsUser(Guid.CreateVersion7());
        var alicePerson = await (await alice.PostJsonAsync(ApiRoutes.People.Create(), new { label = "Confidant" }))
            .ReadJsonAsync<PersonDto>();

        var bob = FakeAuth.CreateClient().AsUser(Guid.CreateVersion7());
        (await (await bob.GetAsync(ApiRoutes.People.Root)).ReadJsonAsync<List<PersonDto>>())!
            .ShouldNotContain(p => p.Label == "Confidant");
        (await bob.PatchJsonAsync(ApiRoutes.People.Rename(alicePerson!.Id), new { label = "Hacked" }))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task the_directory_requires_authentication()
    {
        var anon = FakeAuth.CreateClient();

        (await anon.GetAsync(ApiRoutes.People.Root)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
