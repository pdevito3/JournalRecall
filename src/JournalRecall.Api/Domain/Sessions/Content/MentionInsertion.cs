using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace JournalRecall.Api.Domain.Sessions.Content;

/// <summary>
/// A single approved People tag to insert: the directory <see cref="PersonId"/> + display <see cref="Label"/>
/// to wrap, located by its <see cref="Start"/> offset into the document's derived plaintext (the same
/// coordinate space the AI reads — see <see cref="ProseMirrorToPlainText"/>) and the <see cref="Text"/> the
/// span is expected to cover. The expected text is verified at insert time so a span over since-changed
/// prose is skipped rather than mis-tagged.
/// </summary>
public sealed record MentionSpan(int Start, string Text, Guid PersonId, string Label);

/// <summary>
/// The pure ProseMirror transform that inserts approved People tags into a Content document without a
/// second AI pass (PRD-0006, RICH-008): given a document and a set of approved <see cref="MentionSpan"/>s,
/// it wraps each span's text in a <c>mention</c> node, preserving surrounding text and marks. Pure and
/// deterministic — no DB, no AI. This is what makes AI-proposed tags (RICH-009) trustworthy: approval
/// inserts exactly where proposed.
/// </summary>
/// <remarks>
/// Spans address the derived plaintext that <see cref="ProseMirrorToPlainText"/> produces. A span applies
/// only when its expected <see cref="MentionSpan.Text"/> still sits at its offset <i>within a single text
/// node</i>; spans whose text changed (stale offsets), whose range straddles a block/node boundary or an
/// existing mention, or that overlap an already-applied span are skipped, leaving the document intact.
/// Because every span is resolved against the original document before any node is rebuilt, applying one
/// mention never shifts another's offsets.
/// </remarks>
public static class MentionInsertion
{
    /// <summary>Wraps each applicable span in a mention node, returning a new document. Null-safe.</summary>
    public static JsonNode? Insert(JsonNode? doc, IReadOnlyCollection<MentionSpan> spans)
    {
        if (doc is null || spans.Count == 0)
            return doc;

        // Map each span to a concrete (text node, local range) against the *original* tree, so offsets are
        // resolved once and never shift as mentions are inserted.
        var index = BuildPlaintextIndex(doc);
        var byNode = new Dictionary<JsonObject, List<LocalInsertion>>(ReferenceEqualityComparer.Instance);
        foreach (var span in spans)
            if (TryResolve(index, span, out var node, out var insertion))
                (byNode.TryGetValue(node, out var list) ? list : byNode[node] = []).Add(insertion);

        return Rebuild(doc, byNode);
    }

    /// <summary>Parses, inserts, re-serializes. Blank/invalid JSON is returned unchanged.</summary>
    public static string Insert(string? json, IReadOnlyCollection<MentionSpan> spans)
    {
        if (string.IsNullOrWhiteSpace(json))
            return json ?? string.Empty;

        JsonNode? doc;
        try { doc = JsonNode.Parse(json); }
        catch (JsonException) { return json; }

        return Insert(doc, spans)?.ToJsonString() ?? json;
    }

    // ---- offset → node resolution -------------------------------------------------------------

    /// <summary>One approved insertion localized to a text node: wrap <c>[Start, Start+Length)</c> of its text.</summary>
    private readonly record struct LocalInsertion(int Start, int Length, Guid PersonId, string Label);

    /// <summary>
    /// The derived plaintext exactly as <see cref="ProseMirrorToPlainText"/> renders it, plus, per character,
    /// the originating text node and offset within it (or none, for block separators and existing-mention
    /// labels, which are not wrappable).
    /// </summary>
    private sealed record PlaintextIndex(string Plaintext, IReadOnlyList<(JsonObject? Node, int Local)> Origins);

    private static bool TryResolve(
        PlaintextIndex index, MentionSpan span, out JsonObject node, out LocalInsertion insertion)
    {
        node = null!;
        insertion = default;

        var len = span.Text.Length;
        if (span.Start < 0 || len == 0 || span.Start + len > index.Plaintext.Length)
            return false;
        // Stale span: the prose at this offset is no longer the text the tag was approved against.
        if (!string.Equals(index.Plaintext.Substring(span.Start, len), span.Text, StringComparison.Ordinal))
            return false;

        // Every character of the span must come from the same text node at consecutive offsets — otherwise
        // it straddles a block/node boundary or an existing mention and is skipped.
        var (first, firstLocal) = index.Origins[span.Start];
        if (first is null)
            return false;
        for (var i = 1; i < len; i++)
        {
            var (n, local) = index.Origins[span.Start + i];
            if (!ReferenceEquals(n, first) || local != firstLocal + i)
                return false;
        }

        node = first;
        insertion = new LocalInsertion(firstLocal, len, span.PersonId, span.Label);
        return true;
    }

    /// <summary>
    /// Walks the document mirroring <see cref="ProseMirrorToPlainText"/> (paragraph/heading/codeBlock each
    /// render one line, containers recurse, blocks join with '\n', the result is trimmed) while recording
    /// each emitted character's origin, so a plaintext offset maps back to its text node.
    /// </summary>
    private static PlaintextIndex BuildPlaintextIndex(JsonNode doc)
    {
        var blocks = new List<List<(char Ch, JsonObject? Node, int Local)>>();
        WalkNode(doc, blocks);

        var sb = new StringBuilder();
        var origins = new List<(JsonObject? Node, int Local)>();
        for (var b = 0; b < blocks.Count; b++)
        {
            if (b > 0) { sb.Append('\n'); origins.Add((null, 0)); } // separator: not wrappable
            foreach (var (ch, node, local) in blocks[b]) { sb.Append(ch); origins.Add((node, local)); }
        }

        // ProseMirrorToPlainText trims the joined result; shift origins by the stripped leading whitespace so
        // they line up with the trimmed plaintext callers (and the AI) actually index.
        var full = sb.ToString();
        var lead = full.Length - full.TrimStart().Length;
        var trail = full.Length - full.TrimEnd().Length;
        var plaintext = lead + trail >= full.Length ? string.Empty : full.Substring(lead, full.Length - lead - trail);
        return new PlaintextIndex(plaintext, origins.GetRange(lead, plaintext.Length));
    }

    private static void WalkNode(JsonNode? node, List<List<(char, JsonObject?, int)>> blocks)
    {
        if (node is not JsonObject obj)
            return;

        switch ((obj["type"] as JsonValue)?.GetValue<string>())
        {
            case "paragraph":
            case "heading":
            case "codeBlock":
                var line = new List<(char, JsonObject?, int)>();
                WalkInline(obj["content"], line);
                blocks.Add(line);
                break;
            default:
                if (obj["content"] is JsonArray children)
                    foreach (var child in children)
                        WalkNode(child, blocks);
                break;
        }
    }

    private static void WalkInline(JsonNode? content, List<(char, JsonObject?, int)> line)
    {
        if (content is not JsonArray array)
            return;

        foreach (var child in array)
        {
            if (child is not JsonObject obj)
                continue;

            switch ((obj["type"] as JsonValue)?.GetValue<string>())
            {
                case "text":
                    if (obj["text"] is JsonValue tv && tv.TryGetValue<string>(out var text))
                        for (var i = 0; i < text.Length; i++)
                            line.Add((text[i], obj, i));
                    break;
                case "mention":
                    // An existing mention's label occupies plaintext but is opaque — a new tag can't wrap it.
                    if (obj["attrs"] is JsonObject attrs && attrs["label"] is JsonValue lv &&
                        lv.TryGetValue<string>(out var label))
                        foreach (var ch in label)
                            line.Add((ch, null, 0));
                    break;
                default:
                    WalkInline(obj["content"], line);
                    break;
            }
        }
    }

    // ---- rebuild ------------------------------------------------------------------------------

    /// <summary>Deep-copies the document, splitting each text node that carries insertions into text + mention nodes.</summary>
    private static JsonNode Rebuild(JsonNode doc, Dictionary<JsonObject, List<LocalInsertion>> byNode) =>
        CloneNode(doc, byNode);

    private static JsonNode CloneNode(JsonNode node, Dictionary<JsonObject, List<LocalInsertion>> byNode)
    {
        if (node is not JsonObject obj)
            return node.DeepClone();

        var copy = new JsonObject();
        foreach (var (key, value) in obj)
        {
            if (key == "content" && value is JsonArray content)
                copy["content"] = CloneContent(content, byNode);
            else
                copy[key] = value?.DeepClone();
        }
        return copy;
    }

    private static JsonArray CloneContent(JsonArray content, Dictionary<JsonObject, List<LocalInsertion>> byNode)
    {
        var result = new JsonArray();
        foreach (var child in content)
        {
            if (child is JsonObject obj && byNode.TryGetValue(obj, out var insertions))
                foreach (var piece in SplitTextNode(obj, insertions))
                    result.Add(piece);
            else
                result.Add(child is null ? null : CloneNode(child, byNode));
        }
        return result;
    }

    /// <summary>
    /// Splits a text node into the sequence of text + mention nodes its insertions imply, preserving the
    /// node's marks on the surrounding text. Overlapping insertions are dropped (first one wins).
    /// </summary>
    private static IEnumerable<JsonNode> SplitTextNode(JsonObject textNode, List<LocalInsertion> insertions)
    {
        var text = textNode["text"]!.GetValue<string>();
        var marks = textNode["marks"];
        var pieces = new List<JsonNode>();
        var cursor = 0;

        foreach (var ins in insertions.OrderBy(i => i.Start))
        {
            if (ins.Start < cursor) // overlaps an already-applied span in this node — skip it
                continue;
            if (ins.Start > cursor)
                pieces.Add(TextPiece(text[cursor..ins.Start], marks));
            pieces.Add(MentionNode(ins.PersonId, ins.Label));
            cursor = ins.Start + ins.Length;
        }
        if (cursor < text.Length)
            pieces.Add(TextPiece(text[cursor..], marks));

        return pieces;
    }

    private static JsonObject TextPiece(string text, JsonNode? marks)
    {
        var node = new JsonObject { ["type"] = "text", ["text"] = text };
        if (marks is not null)
            node["marks"] = marks.DeepClone();
        return node;
    }

    private static JsonObject MentionNode(Guid personId, string label) => new()
    {
        ["type"] = "mention",
        ["attrs"] = new JsonObject { ["personId"] = personId.ToString(), ["label"] = label },
    };
}
