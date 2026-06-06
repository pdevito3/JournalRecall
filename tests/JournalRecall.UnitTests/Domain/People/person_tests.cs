using JournalRecall.Api.Domain.People;

namespace JournalRecall.UnitTests.Domain.People;

/// <summary>
/// Unit tests for the <see cref="Person"/> directory aggregate (PRD-0006): label normalization on create
/// and rename, and the validation that a label can't be blank. No host or DB.
/// </summary>
public class person_tests
{
    [Fact]
    public void create_sets_the_owner_and_trims_the_label()
    {
        var userId = Guid.CreateVersion7();

        var person = Person.Create(userId, "  Sam  ");

        person.UserId.ShouldBe(userId);
        person.Label.ShouldBe("Sam");
    }

    [Fact]
    public void rename_replaces_the_label_in_place()
    {
        var person = Person.Create(Guid.CreateVersion7(), "Sam");

        person.Rename("Samuel");

        person.Label.ShouldBe("Samuel");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void a_blank_label_is_rejected(string? label)
    {
        Should.Throw<ArgumentException>(() => Person.Create(Guid.CreateVersion7(), label!));
    }
}
