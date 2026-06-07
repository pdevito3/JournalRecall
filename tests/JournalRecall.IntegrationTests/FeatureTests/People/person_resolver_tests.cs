using JournalRecall.Api.Domain.People;
using JournalRecall.Api.Domain.People.Dtos;
using JournalRecall.Api.Domain.People.Features;

namespace JournalRecall.IntegrationTests.FeatureTests.People;

/// <summary>
/// The <see cref="PersonResolver"/> seam (PRD-0006, RICH-006): a detected name resolves to an existing
/// directory <c>PersonId</c> by exact (case-insensitive, trimmed) match, signals "new" (<c>null</c>) when
/// the directory holds no match, and is scoped per-User so one directory never resolves against another's.
/// </summary>
public class person_resolver_tests : TestBase
{
    [Fact]
    public async Task resolves_an_exact_directory_match_to_its_person_id()
    {
        using var scope = new TestingServiceScope();
        var sam = await scope.SendAsync(new CreatePerson.Command(new PersonForWrite("Sam")));

        var resolved = await scope.GetService<PersonResolver>().ResolveAsync("Sam", default);

        resolved.ShouldBe(sam.Id);
    }

    [Theory]
    [InlineData("  sam  ")] // trimmed + case-insensitive, mirroring the directory's dedup rule
    [InlineData("SAM")]
    public async Task matching_is_case_insensitive_and_trimmed(string name)
    {
        using var scope = new TestingServiceScope();
        var sam = await scope.SendAsync(new CreatePerson.Command(new PersonForWrite("Sam")));

        (await scope.GetService<PersonResolver>().ResolveAsync(name, default)).ShouldBe(sam.Id);
    }

    [Fact]
    public async Task signals_new_when_no_directory_entry_matches()
    {
        using var scope = new TestingServiceScope();
        await scope.SendAsync(new CreatePerson.Command(new PersonForWrite("Sam")));

        (await scope.GetService<PersonResolver>().ResolveAsync("Jordan", default)).ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task a_blank_name_never_matches(string? name)
    {
        using var scope = new TestingServiceScope();
        await scope.SendAsync(new CreatePerson.Command(new PersonForWrite("Sam")));

        (await scope.GetService<PersonResolver>().ResolveAsync(name, default)).ShouldBeNull();
    }

    [Fact]
    public async Task resolution_is_scoped_to_the_owner_directory()
    {
        using var alice = new TestingServiceScope();
        await alice.SendAsync(new CreatePerson.Command(new PersonForWrite("Confidant")));

        using var bob = new TestingServiceScope();
        // Bob's directory is empty, so Alice's "Confidant" is unresolvable for him.
        (await bob.GetService<PersonResolver>().ResolveAsync("Confidant", default)).ShouldBeNull();
    }
}
