using JournalRecall.Api.Domain.People.Dtos;
using JournalRecall.Api.Domain.People.Features;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.Api.Domain.Sessions.Features;

namespace JournalRecall.IntegrationTests.FeatureTests.People;

/// <summary>
/// The per-User Person directory (PRD-0006, RICH-005) at the integration layer: create/list/rename via the
/// MediatR slices, one User's directory invisible to another, and a rename propagating to the People labels
/// of a Session that references the Person by id. No HTTP.
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
        // Setting People by label find-or-creates the directory Person and references it by id.
        await scope.SendAsync(new UpdateMetadata.Command(sessionId, new MetadataForWrite(null, ["Sam"], null)));
        var personId = (await scope.SendAsync(new GetPeople.Query())).Single().Id;

        (await scope.SendAsync(new RenamePerson.Command(personId, new PersonForWrite("Samuel")))).ShouldBeTrue();

        // The Session's People label follows the rename because SessionPerson references the PersonId.
        (await scope.SendAsync(new GetSession.Query(sessionId)))!.People.ShouldBe(["Samuel"]);
    }

    [Fact]
    public async Task setting_the_same_label_twice_reuses_one_directory_entry()
    {
        using var scope = new TestingServiceScope();
        var first = (await scope.SendAsync(new CreateSession.Command(null, null))).Id;
        var second = (await scope.SendAsync(new CreateSession.Command(null, null))).Id;

        await scope.SendAsync(new UpdateMetadata.Command(first, new MetadataForWrite(null, ["Sam"], null)));
        await scope.SendAsync(new UpdateMetadata.Command(second, new MetadataForWrite(null, ["sam"], null)));

        // Case-insensitive find-or-create: a single "Sam" entry backs both Sessions.
        var directory = await scope.SendAsync(new GetPeople.Query());
        directory.Count.ShouldBe(1);
        directory.Single().Label.ShouldBe("Sam");
    }
}
