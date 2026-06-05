using System.Text;
using System.Text.RegularExpressions;

namespace JournalRecall.Api.Domain.Corrections;

/// <summary>
/// Turns a user's Corrections into the two things Cleanup needs: a prompt-context block for hint-mode
/// entries (the model fixes in-context) and a deterministic substitution pass for hard-replace entries
/// (CONTEXT.md). Both touch only the Cleaned copy — Raw is never an input here.
/// </summary>
public static class CorrectionApplier
{
    /// <summary>
    /// Builds the instructions block listing hint-mode Corrections, or null when there are none. Hard-
    /// replace entries are excluded — they are handled deterministically by <see cref="ApplyHardReplacements"/>.
    /// </summary>
    public static string? BuildHintContext(IEnumerable<Correction> corrections)
    {
        var hints = corrections
            .Where(c => !c.HardReplace && c.Mishearings.Count > 0)
            .ToList();
        if (hints.Count == 0)
            return null;

        var sb = new StringBuilder();
        sb.AppendLine(
            "The user keeps a list of Corrections for terms that are often mis-dictated. When the Raw text "
            + "contains one of the mishearings below used in the sense of the canonical term, replace it with "
            + "the canonical spelling in the Cleaned copy. Do not change unrelated words.");
        foreach (var c in hints)
            sb.AppendLine($"- \"{c.CanonicalTerm}\" — commonly misheard as: {string.Join(", ", c.Mishearings)}");

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Deterministically substitutes every hard-replace mishearing with its canonical term, whole-word
    /// and case-insensitive. Returns the input unchanged when no hard-replace Correction applies.
    /// </summary>
    public static string ApplyHardReplacements(string cleaned, IEnumerable<Correction> corrections)
    {
        if (string.IsNullOrEmpty(cleaned))
            return cleaned;

        foreach (var correction in corrections.Where(c => c.HardReplace))
            foreach (var mishearing in correction.Mishearings)
            {
                if (mishearing.Length == 0)
                    continue;
                var pattern = $@"\b{Regex.Escape(mishearing)}\b";
                cleaned = Regex.Replace(cleaned, pattern, correction.CanonicalTerm, RegexOptions.IgnoreCase);
            }

        return cleaned;
    }
}
