using System.Net;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.FunctionalTests.TestUtilities;
using JournalRecall.SharedTestHelpers.Fakes.Sessions;

namespace JournalRecall.FunctionalTests.Sessions;

/// <summary>
/// The People-tag proposal endpoint's HTTP contract (PRD-0006, RICH-009): a Cleanup run surfaces a
/// proposal for review, and POSTing an approval inserts the tag — the Person projects onto the Session and
/// the proposal clears. The resolution branches are covered at the integration layer; this asserts the
/// review→approve UI path end to end over HTTP. Cleanup is driven by the scripted chat client.
/// </summary>
public class people_tag_proposal_endpoint_tests(WebTestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task a_proposal_can_be_reviewed_then_approved_over_http()
    {
        RealAuth.CleanupChat.SuggestPeople = ["Sam"];
        try
        {
            var client = await RealAuth.CreateAuthenticatedClientAsync();
            var created = await client.PostAsync(ApiRoutes.Sessions.Create(), null);
            var id = (await created.ReadJsonAsync<SessionDto>())!.Id;
            // The draft is canonical ProseMirror JSON (what the editor sends), so the derived plaintext —
            // and the AI's Cleaned copy built from it — actually contains the name to tag.
            await client.PutJsonAsync(ApiRoutes.Sessions.Draft(id), new { rawText = ContentDoc.Doc("lunch with Sam") });
            await client.PostAsync(ApiRoutes.Sessions.Cleanup(id), null);

            // The run proposed Sam for review (default: approval required) — nothing tagged yet.
            var afterCleanup = (await (await client.GetAsync(ApiRoutes.Sessions.Get(id))).ReadJsonAsync<SessionDto>())!;
            afterCleanup.PeopleProposals.ShouldHaveSingleItem().Label.ShouldBe("Sam");
            afterCleanup.People.ShouldBeEmpty();

            var respond = await client.PostJsonAsync(
                ApiRoutes.Sessions.PeopleProposalRespond(id),
                new { label = "Sam", approve = true, bindToPersonId = (Guid?)null, createNew = false });
            respond.StatusCode.ShouldBe(HttpStatusCode.NoContent);

            // Approval inserted the tag: Sam projects onto the Session and the proposal is gone.
            var afterApprove = (await (await client.GetAsync(ApiRoutes.Sessions.Get(id))).ReadJsonAsync<SessionDto>())!;
            afterApprove.PeopleProposals.ShouldBeEmpty();
            afterApprove.People.ShouldBe(["Sam"]);
        }
        finally
        {
            RealAuth.CleanupChat.SuggestPeople = [];
        }
    }
}
