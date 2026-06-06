using JournalRecall.Api.Domain.Identity;
using JournalRecall.Api.Exceptions;

namespace JournalRecall.UnitTests.Domain.Identity;

/// <summary>
/// Pure unit tests for the <see cref="Username"/> value object (issue 0026): its throwing
/// <see cref="Username.Create"/> rules and the structural equality it inherits from <c>ValueObject</c>.
/// </summary>
public class username_tests
{
    [Theory]
    [InlineData("alice")]
    [InlineData("a.b_c-1")]
    [InlineData("ABC")]
    [InlineData("user.name_99")]
    public void valid_input_creates_a_username(string input)
    {
        Username.Create(input).Value.ShouldBe(input);
    }

    [Fact]
    public void create_trims_surrounding_whitespace()
    {
        Username.Create("  bob  ").Value.ShouldBe("bob");
    }

    [Theory]
    [InlineData("ab")]      // too short (2)
    [InlineData("a")]
    public void too_short_is_rejected(string input)
    {
        Should.Throw<ValidationException>(() => Username.Create(input))
            .Errors.ShouldContainKey("username");
    }

    [Fact]
    public void too_long_is_rejected()
    {
        var thirtyThree = new string('a', 33);
        Should.Throw<ValidationException>(() => Username.Create(thirtyThree))
            .Errors.ShouldContainKey("username");
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("bad!char")]
    [InlineData("with@sign")]
    [InlineData("slash/here")]
    public void illegal_characters_are_rejected(string input)
    {
        Should.Throw<ValidationException>(() => Username.Create(input))
            .Errors.ShouldContainKey("username");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void null_or_whitespace_is_rejected(string? input)
    {
        Should.Throw<ValidationException>(() => Username.Create(input))
            .Errors.ShouldContainKey("username");
    }

    [Fact]
    public void length_boundaries_are_inclusive()
    {
        Username.Create("abc").Value.ShouldBe("abc");                 // min 3
        Username.Create(new string('a', 32)).Value.Length.ShouldBe(32); // max 32
    }

    [Fact]
    public void two_usernames_with_the_same_value_are_equal()
    {
        var a = Username.Create("alice");
        var b = Username.Create("alice");

        a.ShouldBe(b);
        (a == b).ShouldBeTrue();
        (a != b).ShouldBeFalse();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void usernames_with_different_values_are_not_equal()
    {
        var a = Username.Create("alice");
        var b = Username.Create("bob");

        a.ShouldNotBe(b);
        (a == b).ShouldBeFalse();
        (a != b).ShouldBeTrue();
    }
}
