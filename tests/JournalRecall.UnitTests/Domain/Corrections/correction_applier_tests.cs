using JournalRecall.Api.Domain.Corrections;

namespace JournalRecall.UnitTests.Domain.Corrections;

/// <summary>
/// Pure unit tests for <see cref="CorrectionApplier"/>: the hint-context block (hint-mode entries only)
/// and deterministic hard-replace substitution (whole-word, case-insensitive, hard-replace entries only).
/// </summary>
public class correction_applier_tests
{
    private static readonly Guid User = Guid.CreateVersion7();

    private static Correction Hard(string canonical, params string[] mishearings) =>
        Correction.Create(User, canonical, mishearings, hardReplace: true);

    private static Correction Hint(string canonical, params string[] mishearings) =>
        Correction.Create(User, canonical, mishearings, hardReplace: false);

    [Fact]
    public void hard_replace_substitutes_whole_words_case_insensitively()
    {
        var result = CorrectionApplier.ApplyHardReplacements(
            "I love Prophecy and prophecy.", [Hard("Profisee", "prophecy")]);

        result.ShouldBe("I love Profisee and Profisee.");
    }

    [Fact]
    public void hard_replace_respects_word_boundaries()
    {
        // "prophecying" contains "prophecy" but is a different word — it must not be touched.
        CorrectionApplier.ApplyHardReplacements("prophecying", [Hard("Profisee", "prophecy")])
            .ShouldBe("prophecying");
    }

    [Fact]
    public void hint_mode_corrections_are_not_hard_replaced()
    {
        CorrectionApplier.ApplyHardReplacements("prophecy", [Hint("Profisee", "prophecy")])
            .ShouldBe("prophecy");
    }

    [Fact]
    public void hard_replace_returns_empty_or_unmatched_text_unchanged()
    {
        CorrectionApplier.ApplyHardReplacements("", [Hard("Profisee", "prophecy")]).ShouldBeEmpty();
        CorrectionApplier.ApplyHardReplacements("nothing to fix", [Hard("Profisee", "prophecy")])
            .ShouldBe("nothing to fix");
    }

    [Fact]
    public void hint_context_lists_only_hint_mode_corrections()
    {
        var context = CorrectionApplier.BuildHintContext([
            Hint("Profisee", "prophecy", "prophesy"),
            Hard("Kubernetes", "kubernetis"),
        ]);

        context.ShouldNotBeNull();
        context.ShouldContain("Profisee");
        context.ShouldContain("prophecy");
        context.ShouldNotContain("Kubernetes"); // hard-replace entries are handled deterministically
    }

    [Fact]
    public void hint_context_is_null_when_there_are_no_hint_corrections()
    {
        CorrectionApplier.BuildHintContext([Hard("Kubernetes", "kubernetis")]).ShouldBeNull();
        CorrectionApplier.BuildHintContext([]).ShouldBeNull();
    }
}
