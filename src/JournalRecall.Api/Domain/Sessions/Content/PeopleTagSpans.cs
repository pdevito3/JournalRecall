namespace JournalRecall.Api.Domain.Sessions.Content;

/// <summary>
/// Locates where a proposed Person name should be tagged in a Content document's derived plaintext
/// (PRD-0006, RICH-009). Pure and deterministic — no DB, no AI. Since people-tagging is verbatim-name
/// only (pronoun resolution is out of scope), the AI need only propose <i>names</i>; this module finds
/// each word-boundary occurrence in the Cleaned plaintext, yielding both the <see cref="MentionSpan"/>s
/// that <see cref="MentionInsertion"/> wraps on approval and the sentence previews the review card shows.
/// Working from the live document rather than AI-supplied offsets means a tag always lands exactly where
/// the name actually sits, even though the model emitted Markdown the server later converted to JSON.
/// </summary>
public static class PeopleTagSpans
{
    /// <summary>
    /// Every word-boundary occurrence of <paramref name="name"/> in <paramref name="plaintext"/>
    /// (case-insensitive), as the start offset into the plaintext. A blank name yields none. Boundaries
    /// keep "Sam" from matching inside "Samuel".
    /// </summary>
    public static IReadOnlyList<int> Occurrences(string? plaintext, string? name)
    {
        var text = plaintext ?? string.Empty;
        var needle = name?.Trim() ?? string.Empty;
        if (needle.Length == 0 || text.Length == 0)
            return [];

        var starts = new List<int>();
        var from = 0;
        while (from <= text.Length - needle.Length)
        {
            var at = text.IndexOf(needle, from, StringComparison.OrdinalIgnoreCase);
            if (at < 0)
                break;
            if (IsWordBoundary(text, at, needle.Length))
                starts.Add(at);
            from = at + 1;
        }
        return starts;
    }

    /// <summary>
    /// The <see cref="MentionSpan"/>s wrapping each occurrence of <paramref name="name"/> in the document's
    /// derived plaintext, bound to the resolved <paramref name="personId"/> and display
    /// <paramref name="label"/>. Each span's text is the document's actual substring (preserving its
    /// casing) so <see cref="MentionInsertion"/>'s exact-match check passes.
    /// </summary>
    public static IReadOnlyList<MentionSpan> Spans(string? plaintext, string name, Guid personId, string label)
    {
        var text = plaintext ?? string.Empty;
        return Occurrences(text, name)
            .Select(start => new MentionSpan(start, text.Substring(start, name.Trim().Length), personId, label))
            .ToList();
    }

    /// <summary>
    /// The distinct sentence (or line) around each occurrence of <paramref name="name"/>, in document
    /// order — the per-Person previews the review card shows ("every sentence the AI would tag them in").
    /// Two occurrences in the same sentence collapse to one preview.
    /// </summary>
    public static IReadOnlyList<string> Contexts(string? plaintext, string? name)
    {
        var text = plaintext ?? string.Empty;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var contexts = new List<string>();
        foreach (var start in Occurrences(text, name))
        {
            var sentence = SentenceAround(text, start);
            if (sentence.Length > 0 && seen.Add(sentence))
                contexts.Add(sentence);
        }
        return contexts;
    }

    private static bool IsWordBoundary(string text, int start, int length)
    {
        var before = start - 1;
        var after = start + length;
        var leftOk = before < 0 || !char.IsLetterOrDigit(text[before]);
        var rightOk = after >= text.Length || !char.IsLetterOrDigit(text[after]);
        return leftOk && rightOk;
    }

    /// <summary>Expands an offset out to the surrounding sentence, bounded by terminators and newlines.</summary>
    private static string SentenceAround(string text, int offset)
    {
        var start = offset;
        while (start > 0 && !IsTerminator(text[start - 1]))
            start--;
        var end = offset;
        while (end < text.Length && !IsTerminator(text[end]))
            end++;
        if (end < text.Length) // include the trailing terminator (e.g. the full stop)
            end++;
        return text[start..end].Trim();
    }

    private static bool IsTerminator(char c) => c is '.' or '!' or '?' or '\n' or '\r';
}
