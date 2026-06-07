using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Domain.People.Dtos;
using JournalRecall.Api.Domain.People.Features;
using JournalRecall.Api.Domain.Sessions.Features;
using JournalRecall.Api.Domain.Sessions.Services;
using JournalRecall.SharedTestHelpers.Fakes.Sessions;

namespace JournalRecall.IntegrationTests.FeatureTests.Sessions;

/// <summary>
/// The AI People-tag proposal flow (PRD-0006, RICH-009) at the integration layer: a Cleanup run proposes
/// People for per-Person review (default), resolving exact directory matches and flagging the rest "new";
/// approving inserts mentions deterministically (binding, reassigning, or creating), reject drops the
/// proposal, and with approval turned off the run tags resolved People inline. Driven through the runner +
/// scripted client and RespondToPersonProposal, no HTTP.
/// </summary>
public class people_tag_proposal_tests : TestBase
{
    private async Task<Guid> CleanedSessionWithRaw(TestingServiceScope scope, string raw)
    {
        var session = new FakeSessionBuilder().WithUserId(scope.CurrentUserId).WithRawText(raw).Build();
        await scope.InsertAsync(session);
        await scope.GetService<SessionCleanupRunner>().RunAsync(session.Id);
        return session.Id;
    }

    private static Task RequireApproval(TestingServiceScope scope, bool required) =>
        scope.ExecuteDbContextAsync(async db =>
        {
            var user = await db.Users.FirstAsync(u => u.Id == scope.CurrentUserId);
            user.RequirePeopleTagApproval = required;
            await db.SaveChangesAsync();
        });

    private static Task<List<PersonDto>> Directory(TestingServiceScope scope) =>
        scope.ExecuteDbContextAsync(db => db.People.Select(p => new PersonDto(p.Id, p.Label)).ToListAsync());

    [Fact]
    public async Task a_cleanup_run_proposes_new_people_for_review_by_default()
    {
        using var scope = new TestingServiceScope();
        CleanupChat.SuggestPeople = ["Sam"];
        var id = await CleanedSessionWithRaw(scope, "had a great day with Sam");

        var session = await scope.SendAsync(new GetSession.Query(id));
        // The proposal is pending — nothing tagged, nothing added to the directory yet.
        session!.People.ShouldBeEmpty();
        (await Directory(scope)).ShouldBeEmpty();

        var proposal = session.PeopleProposals.ShouldHaveSingleItem();
        proposal.Label.ShouldBe("Sam");
        proposal.IsNew.ShouldBeTrue();
        proposal.MatchedPersonId.ShouldBeNull();
        proposal.Contexts.ShouldNotBeEmpty(); // the sentence the tag would land in
    }

    [Fact]
    public async Task an_exact_directory_match_auto_links_the_proposal()
    {
        using var scope = new TestingServiceScope();
        var sam = await scope.SendAsync(new CreatePerson.Command(new PersonForWrite("Sam")));
        CleanupChat.SuggestPeople = ["Sam"];
        var id = await CleanedSessionWithRaw(scope, "coffee with Sam");

        var proposal = (await scope.SendAsync(new GetSession.Query(id)))!.PeopleProposals.ShouldHaveSingleItem();
        proposal.IsNew.ShouldBeFalse();
        proposal.MatchedPersonId.ShouldBe(sam.Id);
        proposal.MatchedLabel.ShouldBe("Sam");
    }

    [Fact]
    public async Task approving_a_new_proposal_creates_the_person_tags_the_prose_and_clears_the_proposal()
    {
        using var scope = new TestingServiceScope();
        CleanupChat.SuggestPeople = ["Sam"];
        var id = await CleanedSessionWithRaw(scope, "lunch with Sam");

        (await scope.SendAsync(new RespondToPersonProposal.Command(id, "Sam", Approve: true, BindToPersonId: null, CreateNew: false)))
            .ShouldBeTrue();

        var session = await scope.SendAsync(new GetSession.Query(id));
        session!.PeopleProposals.ShouldBeEmpty();
        session.People.ShouldBe(["Sam"]); // projected from the inserted mention
        (await Directory(scope)).Select(p => p.Label).ShouldBe(["Sam"]); // upserted only on approval
    }

    [Fact]
    public async Task approving_with_a_reassign_binds_to_the_chosen_existing_person()
    {
        using var scope = new TestingServiceScope();
        var samuel = await scope.SendAsync(new CreatePerson.Command(new PersonForWrite("Samuel")));
        CleanupChat.SuggestPeople = ["Sam"]; // proposed "new", but the User reassigns it to Samuel
        var id = await CleanedSessionWithRaw(scope, "ran into Sam");

        (await scope.SendAsync(new RespondToPersonProposal.Command(id, "Sam", Approve: true, BindToPersonId: samuel.Id, CreateNew: false)))
            .ShouldBeTrue();

        var session = await scope.SendAsync(new GetSession.Query(id));
        session!.People.ShouldBe(["Samuel"]);
        (await Directory(scope)).Select(p => p.Label).ShouldBe(["Samuel"]); // no stray "Sam" created
    }

    [Fact]
    public async Task approving_with_create_new_forces_a_new_person_even_when_an_exact_match_exists()
    {
        using var scope = new TestingServiceScope();
        await scope.SendAsync(new CreatePerson.Command(new PersonForWrite("Sam")));
        CleanupChat.SuggestPeople = ["Sam"]; // would auto-link, but the User forces a brand-new entry
        var id = await CleanedSessionWithRaw(scope, "dinner with Sam");

        (await scope.SendAsync(new RespondToPersonProposal.Command(id, "Sam", Approve: true, BindToPersonId: null, CreateNew: true)))
            .ShouldBeTrue();

        (await Directory(scope)).Count(p => p.Label == "Sam").ShouldBe(2); // a second "Sam" was created
    }

    [Fact]
    public async Task rejecting_a_proposal_drops_it_without_tagging_or_creating()
    {
        using var scope = new TestingServiceScope();
        CleanupChat.SuggestPeople = ["Sam"];
        var id = await CleanedSessionWithRaw(scope, "saw Sam");

        (await scope.SendAsync(new RespondToPersonProposal.Command(id, "Sam", Approve: false, BindToPersonId: null, CreateNew: false)))
            .ShouldBeTrue();

        var session = await scope.SendAsync(new GetSession.Query(id));
        session!.PeopleProposals.ShouldBeEmpty();
        session.People.ShouldBeEmpty();
        (await Directory(scope)).ShouldBeEmpty();
    }

    [Fact]
    public async Task with_approval_off_a_cleanup_run_tags_resolved_people_inline()
    {
        using var scope = new TestingServiceScope();
        await RequireApproval(scope, false);
        CleanupChat.SuggestPeople = ["Sam"];
        var id = await CleanedSessionWithRaw(scope, "called Sam");

        var session = await scope.SendAsync(new GetSession.Query(id));
        session!.PeopleProposals.ShouldBeEmpty();        // nothing to review — applied at cleanup time
        session.People.ShouldBe(["Sam"]);                // tagged inline, projected onto the badges
        (await Directory(scope)).Select(p => p.Label).ShouldBe(["Sam"]); // upserted immediately
    }

    [Fact]
    public async Task proposals_are_scoped_to_the_owning_user()
    {
        using var alice = new TestingServiceScope();
        CleanupChat.SuggestPeople = ["Sam"];
        var id = await CleanedSessionWithRaw(alice, "lunch with Sam");

        using var bob = new TestingServiceScope();
        (await bob.SendAsync(new RespondToPersonProposal.Command(id, "Sam", Approve: true, BindToPersonId: null, CreateNew: false)))
            .ShouldBeFalse();

        // Alice's proposal is still pending and untouched.
        (await alice.SendAsync(new GetSession.Query(id)))!.PeopleProposals.ShouldHaveSingleItem();
    }
}
