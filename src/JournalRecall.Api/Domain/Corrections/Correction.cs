namespace JournalRecall.Api.Domain.Corrections;

/// <summary>
/// A per-user known term used to fix mis-dictations during Cleanup (CONTEXT.md). Carries a canonical
/// term (e.g. "Profisee") and its common mishearings (e.g. "prophecy"). Default mode is an AI-context
/// hint (the model fixes in-context); <see cref="HardReplace"/> flags deterministic substitution.
/// Applied only to the Cleaned copy — Raw is never touched. Belongs to exactly one User (Privacy
/// invariant), enforced by the global query filter.
/// </summary>
public sealed class Correction : BaseEntity
{
    private readonly List<string> _mishearings = [];

    public Guid UserId { get; private set; }
    public string CanonicalTerm { get; private set; } = string.Empty;

    /// <summary>
    /// The common mishearings this Correction fixes. Exposed read-only over a backing field so callers
    /// can't mutate the stored list; EF persists it as a primitive collection (JSON column) via the field.
    /// </summary>
    public IReadOnlyList<string> Mishearings => _mishearings;

    /// <summary>When true, mishearings are substituted deterministically rather than left to the model.</summary>
    public bool HardReplace { get; private set; }

    private Correction() { } // EF

    public static Correction Create(Guid userId, string canonicalTerm, IEnumerable<string>? mishearings, bool hardReplace)
    {
        var correction = new Correction { UserId = userId };
        correction.Apply(canonicalTerm, mishearings, hardReplace);
        return correction;
    }

    /// <summary>Updates the canonical term, mishearings, and mode in place.</summary>
    public void Update(string canonicalTerm, IEnumerable<string>? mishearings, bool hardReplace) =>
        Apply(canonicalTerm, mishearings, hardReplace);

    private void Apply(string canonicalTerm, IEnumerable<string>? mishearings, bool hardReplace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalTerm);
        CanonicalTerm = canonicalTerm.Trim();
        // Normalize: trim, drop blanks/dupes (case-insensitive), and drop any that equal the canonical.
        _mishearings.Clear();
        _mishearings.AddRange((mishearings ?? [])
            .Select(m => m?.Trim() ?? string.Empty)
            .Where(m => m.Length > 0 && !m.Equals(CanonicalTerm, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase));
        HardReplace = hardReplace;
    }
}
