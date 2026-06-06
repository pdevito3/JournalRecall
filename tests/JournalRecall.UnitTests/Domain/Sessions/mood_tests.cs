using JournalRecall.Api.Domain.Sessions.Metadata;
using JournalRecall.Api.Exceptions;

namespace JournalRecall.UnitTests.Domain.Sessions;

/// <summary>
/// Unit tests for the <see cref="Mood"/> value object (its SmartEnum backing is a private detail): known
/// moods carry no text, Custom requires free text, parsing is case-insensitive and total, and the
/// known-mood keys exclude Custom.
/// </summary>
public class mood_tests
{
    [Fact]
    public void a_known_mood_has_its_name_as_the_key_and_no_custom_value()
    {
        var mood = Mood.Of("Joyful");

        mood.Key.ShouldBe("Joyful");
        mood.IsCustom.ShouldBeFalse();
        mood.CustomValue.ShouldBeNull();
        mood.Display.ShouldBe("Joyful");
    }

    [Fact]
    public void a_custom_mood_carries_trimmed_free_text_and_displays_it()
    {
        var mood = Mood.Custom("  bittersweet  ");

        mood.Key.ShouldBe("Custom");
        mood.IsCustom.ShouldBeTrue();
        mood.CustomValue.ShouldBe("bittersweet");
        mood.Display.ShouldBe("bittersweet");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void a_custom_mood_without_text_is_rejected(string? text)
    {
        Should.Throw<ArgumentException>(() => Mood.Custom(text!));
        Should.Throw<ValidationException>(() => Mood.Of("Custom", text));
    }

    [Fact]
    public void a_known_mood_ignores_any_supplied_custom_text()
    {
        Mood.Of("Calm", "ignored").CustomValue.ShouldBeNull();
    }

    [Theory]
    [InlineData("Joyful")]
    [InlineData("joyful")]
    [InlineData("  GRATEFUL ")]
    public void parsing_a_known_key_is_case_insensitive_and_trims(string key)
    {
        Mood.Of(key).IsCustom.ShouldBeFalse();
    }

    [Fact]
    public void parsing_an_unknown_key_throws()
    {
        Should.Throw<InvalidSmartEnumPropertyName>(() => Mood.Of("euphoric"));
    }

    [Fact]
    public void tryof_is_total_over_bad_input()
    {
        Mood.TryOf("Joyful", null, out var known).ShouldBeTrue();
        known.Key.ShouldBe("Joyful");

        Mood.TryOf("Custom", "wistful", out var custom).ShouldBeTrue();
        custom.CustomValue.ShouldBe("wistful");

        Mood.TryOf("Custom", null, out _).ShouldBeFalse();   // Custom needs text
        Mood.TryOf("nonsense", null, out _).ShouldBeFalse(); // unknown key
        Mood.TryOf(null, null, out _).ShouldBeFalse();       // no key
        Mood.TryOf("  ", null, out _).ShouldBeFalse();       // blank key
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
        Mood.Of("Joyful").ShouldBe(Mood.Of("Joyful"));
        Mood.Custom("a").ShouldNotBe(Mood.Custom("b"));
    }
}
