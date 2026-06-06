using JournalRecall.Api.Domain.Sessions.Metadata;

namespace JournalRecall.UnitTests.Domain.Sessions;

/// <summary>
/// Unit tests for the <see cref="Mood"/> value object (its SmartEnum backing is a private detail):
/// single-string resolution to known-vs-custom (PRD-0006), the canonical <see cref="Mood.Value"/> a Mood
/// serializes to (never the "Custom" sentinel), case-insensitive/total parsing, and the known-mood keys.
/// </summary>
public class mood_tests
{
    [Fact]
    public void resolving_a_known_name_yields_a_known_mood_valued_by_its_name()
    {
        var mood = Mood.Resolve("Joyful");

        mood.IsCustom.ShouldBeFalse();
        mood.CustomValue.ShouldBeNull();
        mood.Value.ShouldBe("Joyful");
        mood.Display.ShouldBe("Joyful");
    }

    [Fact]
    public void resolving_an_unknown_string_yields_a_custom_mood_valued_by_its_text()
    {
        var mood = Mood.Resolve("  bittersweet  ");

        mood.IsCustom.ShouldBeTrue();
        mood.CustomValue.ShouldBe("bittersweet");
        mood.Value.ShouldBe("bittersweet"); // never the "Custom" sentinel
    }

    [Theory]
    [InlineData("Joyful")]
    [InlineData("joyful")]
    [InlineData("  GRATEFUL ")]
    public void resolving_a_known_name_is_case_insensitive_and_trims(string name)
    {
        Mood.Resolve(name).IsCustom.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void resolving_blank_input_is_rejected(string? text)
    {
        Should.Throw<ArgumentException>(() => Mood.Resolve(text!));
    }

    [Fact]
    public void tryresolve_is_total_over_bad_input()
    {
        Mood.TryResolve("Joyful", out var known).ShouldBeTrue();
        known.Value.ShouldBe("Joyful");

        Mood.TryResolve("wistful", out var custom).ShouldBeTrue();
        custom.IsCustom.ShouldBeTrue();
        custom.Value.ShouldBe("wistful");

        Mood.TryResolve(null, out _).ShouldBeFalse();   // no value
        Mood.TryResolve("  ", out _).ShouldBeFalse();   // blank value
    }

    [Fact]
    public void known_keys_are_the_ten_pickable_moods_excluding_custom()
    {
        Mood.KnownKeys.Count.ShouldBe(10);
        Mood.KnownKeys.ShouldNotContain("Custom");
        Mood.KnownKeys.ShouldContain("Joyful");
    }

    [Fact]
    public void moods_have_value_equality()
    {
        Mood.Resolve("Joyful").ShouldBe(Mood.Resolve("joyful")); // canonicalized to the same known mood
        Mood.Custom("a").ShouldNotBe(Mood.Custom("b"));
    }
}
