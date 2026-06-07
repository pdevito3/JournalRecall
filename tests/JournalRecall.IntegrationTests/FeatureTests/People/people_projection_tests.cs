using JournalRecall.Api.Domain.People.Dtos;
using JournalRecall.Api.Domain.People.Features;
using JournalRecall.Api.Domain.Sessions.Features;
using JournalRecall.SharedTestHelpers.Fakes.Sessions;

namespace JournalRecall.IntegrationTests.FeatureTests.People;

/// <summary>
/// People as a projection of the prose (PRD-0006, RICH-006/007) at the integration layer: saving Raw or
/// Cleaned content reconciles a Session's People to the union of its @-mentions, so the People badges track
/// the writing. The Person directory is populated explicitly (the inline-create path); mentions reference
/// People by id. No HTTP.
/// </summary>
public class people_projection_tests : TestBase
{
    private static async Task<Guid> NewSession(TestingServiceScope scope) =>
        (await scope.SendAsync(new CreateSession.Command(null, null))).Id;

    private static Task<PersonDto> NewPerson(TestingServiceScope scope, string label) =>
        scope.SendAsync(new CreatePerson.Command(new PersonForWrite(label)));

    [Fact]
    public async Task mentioning_a_person_in_raw_tags_them_on_the_session()
    {
        using var scope = new TestingServiceScope();
        var id = await NewSession(scope);
        var sam = await NewPerson(scope, "Sam");

        await scope.SendAsync(new SaveDraft.Command(id, ContentDoc.DocWithMentions((sam.Id, "Sam"))));

        (await scope.SendAsync(new GetSession.Query(id)))!.People.ShouldBe(["Sam"]);
    }

    [Fact]
    public async Task people_are_the_union_of_raw_and_cleaned_mentions()
    {
        using var scope = new TestingServiceScope();
        var id = await NewSession(scope);
        var sam = await NewPerson(scope, "Sam");
        var alex = await NewPerson(scope, "Alex");

        await scope.SendAsync(new SaveDraft.Command(id, ContentDoc.DocWithMentions((sam.Id, "Sam"))));
        await scope.SendAsync(new SaveCleaned.Command(id, ContentDoc.DocWithMentions((alex.Id, "Alex"))));

        // Sam (Raw) + Alex (Cleaned) — the union, even though neither copy mentions both.
        (await scope.SendAsync(new GetSession.Query(id)))!.People.ShouldBe(["Alex", "Sam"], ignoreOrder: true);
    }

    [Fact]
    public async Task removing_a_mention_untags_the_person()
    {
        using var scope = new TestingServiceScope();
        var id = await NewSession(scope);
        var sam = await NewPerson(scope, "Sam");

        await scope.SendAsync(new SaveDraft.Command(id, ContentDoc.DocWithMentions((sam.Id, "Sam"))));
        (await scope.SendAsync(new GetSession.Query(id)))!.People.ShouldBe(["Sam"]);

        await scope.SendAsync(new SaveDraft.Command(id, ContentDoc.Doc("Sam is gone now"))); // no mention
        (await scope.SendAsync(new GetSession.Query(id)))!.People.ShouldBeEmpty();
    }

    [Fact]
    public async Task projected_people_surface_on_the_timeline_row()
    {
        using var scope = new TestingServiceScope();
        var id = await NewSession(scope);
        var sam = await NewPerson(scope, "Sam");

        await scope.SendAsync(new SaveDraft.Command(id, ContentDoc.DocWithMentions((sam.Id, "Sam"))));

        var row = (await scope.SendAsync(new GetSessionList.Query(null))).Single(s => s.Id == id);
        row.People.ShouldBe(["Sam"]);
    }
}
