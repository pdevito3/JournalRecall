using System.Text.Json.Nodes;
using JournalRecall.Api.Domain.People.Dtos;
using JournalRecall.Api.Domain.People.Features;
using JournalRecall.Api.Domain.Sessions.Features;
using JournalRecall.SharedTestHelpers.Fakes.Sessions;
using Microsoft.EntityFrameworkCore;

namespace JournalRecall.IntegrationTests.FeatureTests.People;

/// <summary>
/// The per-User Person directory (PRD-0006, RICH-005) at the integration layer: create/list/rename via the
/// MediatR slices, one User's directory invisible to another, and a rename propagating to the People labels
/// of a Session that @-mentions the Person by id. No HTTP.
/// </summary>
public class person_directory_tests : TestBase
{
    [Fact]
    public async Task created_people_are_listed_for_the_owner_sorted_by_label()
    {
        using var scope = new TestingServiceScope();
        await scope.SendAsync(new CreatePerson.Command(new PersonForWrite("Sam")));
        await scope.SendAsync(new CreatePerson.Command(new PersonForWrite("Alex")));

        var people = await scope.SendAsync(new GetPeople.Query());

        people.Select(p => p.Label).ShouldBe(["Alex", "Sam"]);
    }

    [Fact]
    public async Task a_directory_is_private_to_its_owner()
    {
        using var alice = new TestingServiceScope();
        var alicePerson = await alice.SendAsync(new CreatePerson.Command(new PersonForWrite("Confidant")));

        using var bob = new TestingServiceScope();
        (await bob.SendAsync(new GetPeople.Query())).ShouldBeEmpty();
        // Bob cannot rename a Person that doesn't exist in his directory.
        (await bob.SendAsync(new RenamePerson.Command(alicePerson.Id, new PersonForWrite("Hacked"))))
            .ShouldBeFalse();

        // Alice's entry is untouched.
        (await alice.SendAsync(new GetPeople.Query())).Single().Label.ShouldBe("Confidant");
    }

    [Fact]
    public async Task renaming_a_person_propagates_to_sessions_that_reference_it()
    {
        using var scope = new TestingServiceScope();
        var sessionId = (await scope.SendAsync(new CreateSession.Command(null, null))).Id;
        // A Person is created explicitly (the @-mention inline-create path) and referenced by a mention
        // in the prose; saving the draft reconciles the SessionPerson to the mentioned id.
        var sam = await scope.SendAsync(new CreatePerson.Command(new PersonForWrite("Sam")));
        await scope.SendAsync(new SaveDraft.Command(sessionId, ContentDoc.DocWithMentions((sam.Id, "Sam"))));

        (await scope.SendAsync(new RenamePerson.Command(sam.Id, new PersonForWrite("Samuel")))).ShouldBeTrue();

        // The Session's People label follows the rename because SessionPerson references the PersonId.
        (await scope.SendAsync(new GetSession.Query(sessionId)))!.People.ShouldBe(["Samuel"]);
    }

    [Fact]
    public async Task renaming_a_person_rewrites_mention_labels_in_raw_and_cleaned_prose_and_plaintext()
    {
        using var scope = new TestingServiceScope();
        var sessionId = (await scope.SendAsync(new CreateSession.Command(null, null))).Id;
        var sam = await scope.SendAsync(new CreatePerson.Command(new PersonForWrite("Sam")));
        // The Person is @-mentioned in both the Raw and the Cleaned copy, with the old label snapshotted in
        // each mention node (exactly what the editor saves).
        await scope.SendAsync(new SaveDraft.Command(sessionId, ContentDoc.DocWithMentions((sam.Id, "Sam"))));
        await scope.SendAsync(new SaveCleaned.Command(sessionId, ContentDoc.DocWithMentions((sam.Id, "Sam"))));

        (await scope.SendAsync(new RenamePerson.Command(sam.Id, new PersonForWrite("Samuel")))).ShouldBeTrue();

        var dto = (await scope.SendAsync(new GetSession.Query(sessionId)))!;
        // (a) The mention label attr is rewritten in both stored prose copies.
        MentionLabel(dto.RawDraft).ShouldBe("Samuel");
        MentionLabel(dto.CleanedDraft).ShouldBe("Samuel");
        // (b) The projected People badge shows the new label.
        dto.People.ShouldBe(["Samuel"]);
        // (c) The derived plaintext projections — which feed search and AI input — reflect the new label.
        ContentDoc.PlainText(dto.RawDraft).ShouldBe("Samuel");
        ContentDoc.PlainText(dto.CleanedDraft).ShouldBe("Samuel");
        // The stored *PlainText columns were re-derived in lockstep, not just the read-time projection.
        var stored = await scope.ExecuteDbContextAsync(db => db.Sessions.AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => new { s.RawPlainText, s.CleanedPlainText })
            .SingleAsync());
        stored.RawPlainText.ShouldBe("Samuel");
        stored.CleanedPlainText.ShouldBe("Samuel");
    }

    private static string? MentionLabel(string json) =>
        JsonNode.Parse(json)!["content"]![0]!["content"]![0]!["attrs"]!["label"]!.GetValue<string>();
}
