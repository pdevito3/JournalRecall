using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace JournalRecall.Api.Domain.Sessions.Content;

/// <summary>
/// The one pure seam from canonical <b>Content</b> (ProseMirror/tiptap JSON) back to words: turns a
/// document into the derived plaintext used by the search index and as the AI Cleanup input. Pure and
/// deterministic — no I/O, no DB. Marks (bold/italic/code) are stripped, <c>mention</c> nodes render
/// their <c>attrs.label</c>, and the document never throws on empty, whitespace-only, null, or
/// malformed/unknown input.
/// </summary>
/// <remarks>
/// Projection scheme: each <i>block</i> node (paragraph, heading, listItem, blockquote, codeBlock,
/// and each list as a whole) renders to one line; blocks are joined with a single newline ('\n').
/// Inline nodes (text, mention) within a block concatenate with no separator — the rich layer already
/// carries explicit spaces in the text. The final result is trimmed so empty/whitespace docs yield "".
/// Numbering and bullets are intentionally omitted: the goal is faithful words for search, not visual
/// fidelity.
/// </remarks>
public static class ProseMirrorToPlainText
{
    /// <summary>Renders a canonical Content document to derived plaintext. Null-safe; never throws.</summary>
    public static string Render(JsonNode? doc)
    {
        if (doc is null)
            return string.Empty;

        var blocks = new List<string>();
        RenderNode(doc, blocks);
        return string.Join('\n', blocks).Trim();
    }

    /// <summary>Parses Content JSON, then renders it. Null/blank/invalid JSON yields "" rather than throwing.</summary>
    public static string Render(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return string.Empty;

        JsonNode? doc;
        try
        {
            doc = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return string.Empty;
        }

        return Render(doc);
    }

    /// <summary>
    /// Walks a node, appending each block it produces to <paramref name="blocks"/>. Inline content is
    /// flattened into the current block by <see cref="RenderInline"/>; container blocks recurse so their
    /// children become their own lines. Unknown node types recurse into <c>content</c> if present.
    /// </summary>
    private static void RenderNode(JsonNode? node, List<string> blocks)
    {
        if (node is not JsonObject obj)
            return;

        var type = (obj["type"] as JsonValue)?.GetValue<string>();
        switch (type)
        {
            case "paragraph":
            case "heading":
            {
                var line = new StringBuilder();
                RenderInline(obj["content"], line);
                blocks.Add(line.ToString());
                break;
            }
            case "codeBlock":
            {
                // codeBlock children are text nodes; flatten them to one line of code text.
                var line = new StringBuilder();
                RenderInline(obj["content"], line);
                blocks.Add(line.ToString());
                break;
            }
            case "listItem":
            case "blockquote":
            case "bulletList":
            case "orderedList":
            case "doc":
            default:
                // Containers (and unknown types) recurse: each child block becomes its own line.
                RenderChildren(obj["content"], blocks);
                break;
        }
    }

    private static void RenderChildren(JsonNode? content, List<string> blocks)
    {
        if (content is not JsonArray array)
            return;

        foreach (var child in array)
            RenderNode(child, blocks);
    }

    /// <summary>
    /// Flattens inline content (text + mention, plus any unknown inline that nests text) into a single
    /// line buffer. Marks are ignored — only the words contribute.
    /// </summary>
    private static void RenderInline(JsonNode? content, StringBuilder line)
    {
        if (content is not JsonArray array)
            return;

        foreach (var child in array)
        {
            if (child is not JsonObject obj)
                continue;

            var type = (obj["type"] as JsonValue)?.GetValue<string>();
            switch (type)
            {
                case "text":
                    if (obj["text"] is JsonValue textValue && textValue.TryGetValue<string>(out var text))
                        line.Append(text);
                    break;
                case "mention":
                    if (obj["attrs"] is JsonObject attrs &&
                        attrs["label"] is JsonValue labelValue &&
                        labelValue.TryGetValue<string>(out var label))
                        line.Append(label);
                    break;
                default:
                    // Unknown inline: recover any nested text so no words are lost.
                    RenderInline(obj["content"], line);
                    break;
            }
        }
    }
}
