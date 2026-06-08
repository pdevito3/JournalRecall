using JournalRecall.Api.Domain.Sessions.Metadata;

namespace JournalRecall.UnitTests.Domain.Sessions;

/// <summary>
/// Unit tests for the <see cref="Activity"/> value object (its SmartEnum backing is a private detail,
/// PRD-0007): single-string resolution to known-vs-custom in canonical case, the canonical
/// <see cref="Activity.Value"/> a custom activity serializes to (never the "Custom" sentinel), blank/absent
/// input yielding the <see cref="Activity.None"/> zero value, and None being distinct from Stationary.
/// </summary>
public class activity_tests
{
    [Fact]
    public void resolving_a_known_name_yields_a_known_activity_valued_by_its_canonical_name()
    {
        var activity = Activity.Resolve("Walking");

        activity.IsKnown.ShouldBeTrue();
        activity.IsCustom.ShouldBeFalse();
        activity.IsNone.ShouldBeFalse();
        activity.Value.ShouldBe("Walking");
    }

    [Theory]
    [InlineData("walking", "Walking")]
    [InlineData("  STATIONARY ", "Stationary")]
    [InlineData("resting", "Resting")]
    public void resolving_a_known_name_is_case_insensitive_and_canonicalizes(string input, string canonical)
    {
        var activity = Activity.Resolve(input);

        activity.IsKnown.ShouldBeTrue();
        activity.Value.ShouldBe(canonical);
    }

    [Fact]
    public void resolving_an_unknown_string_yields_a_custom_activity_valued_by_its_text()
    {
        var activity = Activity.Resolve("  cooking  ");

        activity.IsCustom.ShouldBeTrue();
        activity.IsKnown.ShouldBeFalse();
        activity.Value.ShouldBe("cooking"); // trimmed, never the "Custom" sentinel
    }

    [Fact]
    public void the_literal_custom_sentinel_is_never_a_known_member()
    {
        // Typing "Custom" is just a custom activity carrying that word — never the internal sentinel.
        var activity = Activity.Resolve("Custom");

        activity.IsCustom.ShouldBeTrue();
        activity.Value.ShouldBe("Custom");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void blank_or_absent_input_resolves_to_none(string? input)
    {
        var activity = Activity.Resolve(input);

        activity.IsNone.ShouldBeTrue();
        activity.Value.ShouldBe("None");
        activity.ShouldBe(Activity.None);
    }

    [Fact]
    public void none_is_the_default_zero_value_distinct_from_stationary()
    {
        Activity.None.IsNone.ShouldBeTrue();
        Activity.None.IsKnown.ShouldBeTrue(); // None is a known member, not a custom value

        var stationary = Activity.Resolve("Stationary");
        stationary.IsNone.ShouldBeFalse();
        stationary.ShouldNotBe(Activity.None);
    }

    [Fact]
    public void known_keys_include_none_and_exclude_the_custom_sentinel()
    {
        Activity.KnownKeys.ShouldBe(
            ["None", "Stationary", "Walking", "Eating", "Commuting", "Exercising", "Resting"]);
        Activity.KnownKeys.ShouldNotContain("Custom");
    }

    [Fact]
    public void activities_have_value_equality()
    {
        Activity.Resolve("Walking").ShouldBe(Activity.Resolve("walking")); // canonicalized to the same member
        Activity.Custom("a").ShouldNotBe(Activity.Custom("b"));
    }
}
