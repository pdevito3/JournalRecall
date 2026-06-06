using JournalRecall.Api.Domain.Corrections;

namespace JournalRecall.UnitTests.Domain.Corrections;

/// <summary>
/// Unit tests for the <see cref="Correction"/> aggregate: mishearing normalization (trim, drop blanks,
/// case-insensitive de-dupe, drop any equal to the canonical term) and read-only exposure of the list.
/// </summary>
public class correction_tests
{
    private static readonly Guid User = Guid.CreateVersion7();

    [Fact]
    public void mishearings_are_trimmed_deduped_and_stripped_of_the_canonical_term()
    {
        var correction = Correction.Create(
            User, "  Profisee  ",
            ["Prophecy", " prophecy ", "Profisee", "", "  ", "Prof I see"],
            hardReplace: false);

        correction.CanonicalTerm.ShouldBe("Profisee");                 // trimmed
        correction.Mishearings.ShouldBe(["Prophecy", "Prof I see"]);   // deduped (case-insensitive), blanks + canonical dropped
        correction.HardReplace.ShouldBeFalse();
    }

    [Fact]
    public void a_null_mishearing_list_yields_an_empty_set()
    {
        Correction.Create(User, "Profisee", null, hardReplace: true).Mishearings.ShouldBeEmpty();
    }

    [Fact]
    public void update_replaces_the_mishearings_and_mode()
    {
        var correction = Correction.Create(User, "Profisee", ["prophecy"], hardReplace: false);

        correction.Update("Kubernetes", ["kubernetis", "kubernetes"], hardReplace: true);

        correction.CanonicalTerm.ShouldBe("Kubernetes");
        correction.Mishearings.ShouldBe(["kubernetis"]); // "kubernetes" == canonical (case-insensitive) → dropped
        correction.HardReplace.ShouldBeTrue();
    }

    [Fact]
    public void a_blank_canonical_term_is_rejected()
    {
        Should.Throw<ArgumentException>(() => Correction.Create(User, "   ", ["x"], hardReplace: false));
    }

    [Fact]
    public void the_exposed_mishearings_are_read_only()
    {
        var correction = Correction.Create(User, "Profisee", ["prophecy"], hardReplace: false);
        // The exposure is IReadOnlyList — callers cannot mutate the stored list.
        correction.Mishearings.ShouldBeAssignableTo<IReadOnlyList<string>>();
    }
}
