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
    public Guid UserId { get; private set; }
    public string CanonicalTerm { get; private set; } = string.Empty;

    /// <summary>The common mishearings this Correction fixes. Stored as a primitive collection (JSON column).</summary>
    public List<string> Mishearings { get; private set; } = [];

    /// <summary>When true, mishearings are substituted deterministically rather than left to the model.</summary>
    public bool HardReplace { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

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
        Mishearings = (mishearings ?? [])
            .Select(m => m?.Trim() ?? string.Empty)
            .Where(m => m.Length > 0 && !m.Equals(CanonicalTerm, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        HardReplace = hardReplace;
    }
}
