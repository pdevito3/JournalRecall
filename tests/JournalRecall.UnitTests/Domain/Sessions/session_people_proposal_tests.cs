using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Domain.Sessions.Metadata;
using static JournalRecall.SharedTestHelpers.Fakes.Sessions.ContentDoc;

namespace JournalRecall.UnitTests.Domain.Sessions;

/// <summary>
/// Unit tests for the <see cref="Session"/> aggregate's People-tag proposal state (PRD-0006, RICH-009):
/// a Cleanup run replaces the pending proposals, each is removed once reviewed, and applying an approved
/// mention insertion appends a Cleaned Revision + reconciles the People badges from the prose without
/// touching the hand-edit flag (an approved AI tag is not a free-form hand-edit). No host or DB.
/// </summary>
public class session_people_proposal_tests
{
    private static Session Cleaned(string cleanedText)
    {
        var s = Session.Create(Guid.CreateVersion7());
        s.SaveDraft(Doc("raw words"));
        s.BeginCleanup();
        s.CompleteCleanup(Doc(cleanedText), "A short recap.");
        return s;
    }

    [Fact]
    public void replacing_proposals_swaps_the_pending_set()
    {
        var s = Cleaned("clean");
        s.ReplacePeopleProposals([new PersonTagProposal("Sam", null), new PersonTagProposal("Mara", Guid.CreateVersion7())]);
        s.PeopleProposals.Select(p => p.Label).ShouldBe(["Sam", "Mara"]);

        s.ReplacePeopleProposals([new PersonTagProposal("Jo", null)]);
        s.PeopleProposals.Select(p => p.Label).ShouldBe(["Jo"]);
    }

    [Fact]
    public void removing_a_proposal_is_case_insensitive_and_reports_missing()
    {
        var s = Cleaned("clean");
        s.ReplacePeopleProposals([new PersonTagProposal("Sam", null)]);

        s.RemovePersonProposal("sam").ShouldBeTrue();
        s.PeopleProposals.ShouldBeEmpty();
        s.RemovePersonProposal("Sam").ShouldBeFalse();
    }

    [Fact]
    public void applying_cleaned_mentions_appends_a_revision_and_projects_people_without_flagging_handedits()
    {
        var s = Cleaned("I saw Sam today");
        s.CleanedRevisions.Count.ShouldBe(1);
        var personId = Guid.CreateVersion7();

        s.ApplyCleanedMentions(DocWithMentions((personId, "Sam")));

        s.CleanedRevisions.Count.ShouldBe(2);                 // the approved tag is snapshotted (ADR-0003)
        s.People.Select(p => p.PersonId).ShouldBe([personId]); // reconciled from the prose
        s.CleanedHasHandEdits.ShouldBeFalse();                 // approval is not a free-form hand-edit
    }

    [Fact]
    public void applying_an_unchanged_cleaned_copy_mints_no_revision()
    {
        var s = Cleaned("nothing to tag here");
        s.ApplyCleanedMentions(s.CleanedDraft);
        s.CleanedRevisions.Count.ShouldBe(1);
    }
}
