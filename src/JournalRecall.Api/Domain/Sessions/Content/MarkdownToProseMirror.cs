using System.Text;
using System.Text.Json.Nodes;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace JournalRecall.Api.Domain.Sessions.Content;

/// <summary>
/// Pure, deterministic conversion from AI-emitted markdown to canonical Content
/// (ProseMirror/tiptap JSON), restricted to a small node/mark set: paragraph, heading (levels 1-3),
/// bulletList/orderedList/listItem, blockquote, codeBlock, and the text node with bold/italic/code
/// marks. The AI is never asked to emit schema-valid editor JSON — it emits markdown and the server
/// converts here. No DB, no network. Mention insertion is out of scope (RICH-008).
///
/// Unsupported markdown never throws: it degrades to the nearest supported node. The degrade choices
/// are documented inline at each call site.
/// </summary>
public static class MarkdownToProseMirror
{
    // A pipeline with tables enabled so a table parses as a Table block we can flatten to text
    // (rather than being left as ambiguous paragraph text). Everything else stays CommonMark.
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UsePipeTables().UseGridTables().Build();

    /// <summary>Converts markdown to the canonical Content doc node. Never returns null; never throws.</summary>
    public static JsonNode Convert(string? markdown)
    {
        var content = new JsonArray();
        var doc = new JsonObject { ["type"] = "doc", ["content"] = content };

        // Empty/whitespace/null markdown → an empty doc, not an error.
        if (string.IsNullOrWhiteSpace(markdown))
            return doc;

        var parsed = Markdown.Parse(markdown, Pipeline);
        foreach (var block in parsed)
            AppendBlock(content, block);

        return doc;
    }

    /// <summary>Serialized-JSON convenience over <see cref="Convert"/>.</summary>
    public static string ConvertToJson(string? markdown) => Convert(markdown).ToJsonString();

    // ---- blocks -------------------------------------------------------------------------------

    private static void AppendBlock(JsonArray target, Block block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                target.Add(Heading(heading));
                break;

            case ParagraphBlock paragraph:
                target.Add(Paragraph(paragraph.Inline));
                break;

            case QuoteBlock quote:
                target.Add(Blockquote(quote));
                break;

            case ListBlock list:
                target.Add(List(list));
                break;

            case CodeBlock code: // covers both FencedCodeBlock and indented CodeBlock
                target.Add(CodeBlockNode(code));
                break;

            // Thematic break (---/***) has no supported equivalent: drop it silently rather than
            // emit a stray empty paragraph.
            case ThematicBreakBlock:
                break;

            // Raw HTML block degrades to a paragraph carrying its literal text, so words survive.
            case HtmlBlock html:
                AddParagraphFromText(target, RawLines(html));
                break;

            // Tables (extension) degrade to a paragraph per row, cells joined by spaces — keeps the
            // words and avoids dropping content, without inventing a table node we don't support.
            case Table table:
                AppendTableAsParagraphs(target, table);
                break;

            // Any other container we don't model (e.g. footnotes): recurse so nested supported
            // blocks still surface; anything truly unknown is skipped.
            case ContainerBlock container:
                foreach (var child in container)
                    AppendBlock(target, child);
                break;

            // A leaf block we don't recognize → paragraph of its raw text, never thrown away.
            case LeafBlock leaf when leaf.Inline is not null:
                target.Add(Paragraph(leaf.Inline));
                break;
        }
    }

    private static JsonObject Heading(HeadingBlock heading)
    {
        // Clamp markdown h4-h6 down to level 3 — our schema only allows 1..3.
        var level = Math.Clamp(heading.Level, 1, 3);
        return new JsonObject
        {
            ["type"] = "heading",
            ["attrs"] = new JsonObject { ["level"] = level },
            ["content"] = Inlines(heading.Inline),
        };
    }

    private static JsonObject Paragraph(ContainerInline? inline) =>
        new() { ["type"] = "paragraph", ["content"] = Inlines(inline) };

    private static JsonObject Blockquote(QuoteBlock quote)
    {
        var content = new JsonArray();
        foreach (var child in quote)
            AppendBlock(content, child);
        return new JsonObject { ["type"] = "blockquote", ["content"] = content };
    }

    private static JsonObject List(ListBlock list)
    {
        var isOrdered = list.IsOrdered;
        var items = new JsonArray();
        foreach (var child in list)
        {
            if (child is not ListItemBlock itemBlock)
                continue;

            var itemContent = new JsonArray();
            foreach (var inner in itemBlock)
                AppendBlock(itemContent, inner); // paragraphs + nested lists nest inside the listItem
            items.Add(new JsonObject { ["type"] = "listItem", ["content"] = itemContent });
        }

        if (!isOrdered)
            return new JsonObject { ["type"] = "bulletList", ["content"] = items };

        // Carry the ordered-list start (markdown "3." → start:3); default to 1.
        var start = int.TryParse(list.OrderedStart, out var parsed) ? parsed : 1;
        return new JsonObject
        {
            ["type"] = "orderedList",
            ["attrs"] = new JsonObject { ["start"] = start },
            ["content"] = items,
        };
    }

    private static JsonObject CodeBlockNode(CodeBlock code)
    {
        var text = string.Join("\n", code.Lines.Lines.Take(code.Lines.Count).Select(l => l.ToString()));
        return new JsonObject
        {
            ["type"] = "codeBlock",
            ["content"] = new JsonArray { Text(text) },
        };
    }

    private static void AppendTableAsParagraphs(JsonArray target, Table table)
    {
        foreach (var rowObj in table)
        {
            if (rowObj is not TableRow row)
                continue;

            var sb = new StringBuilder();
            foreach (var cellObj in row)
            {
                if (cellObj is not TableCell cell)
                    continue;
                var cellText = PlainTextOfBlocks(cell);
                if (cellText.Length == 0)
                    continue;
                if (sb.Length > 0)
                    sb.Append(' ');
                sb.Append(cellText);
            }

            var line = sb.ToString().Trim();
            if (line.Length > 0)
                AddParagraphFromText(target, line);
        }
    }

    private static void AddParagraphFromText(JsonArray target, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        target.Add(new JsonObject
        {
            ["type"] = "paragraph",
            ["content"] = new JsonArray { Text(text) },
        });
    }

    // ---- inlines ------------------------------------------------------------------------------

    private static JsonArray Inlines(ContainerInline? container)
    {
        var array = new JsonArray();
        if (container is null)
            return array;

        foreach (var inline in container)
            AppendInline(array, inline, marks: null);
        return array;
    }

    // marks: the active mark set inherited from enclosing emphasis/code wrappers.
    private static void AppendInline(JsonArray target, Inline inline, HashSet<string>? marks)
    {
        switch (inline)
        {
            case LiteralInline literal:
                AddText(target, literal.Content.ToString(), marks);
                break;

            // A soft/hard line break inside a paragraph becomes a space so words don't run together.
            case LineBreakInline:
                AddText(target, " ", marks);
                break;

            case CodeInline code:
                AddText(target, code.Content, With(marks, "code"));
                break;

            case EmphasisInline emphasis:
            {
                // Markdig: 1 delimiter → italic, 2 → bold (delimiter char ~/= are other extensions).
                var mark = emphasis.DelimiterCount >= 2 ? "bold" : "italic";
                var nested = With(marks, mark);
                foreach (var child in emphasis)
                    AppendInline(target, child, nested);
                break;
            }

            // Links degrade to their visible text; images degrade to their alt text. Either way the
            // words survive and no link/image node is emitted.
            case LinkInline link:
            {
                if (link.IsImage)
                {
                    AddText(target, PlainTextOf(link), marks); // alt text (the link's children)
                }
                else
                {
                    foreach (var child in link)
                        AppendInline(target, child, marks);
                }
                break;
            }

            // Raw inline HTML (e.g. <span>) degrades to its literal text rather than being dropped.
            case HtmlInline html:
                AddText(target, html.Tag, marks);
                break;

            // Any other container inline (e.g. an extension wrapper) — recurse to keep its content.
            case ContainerInline other:
                foreach (var child in other)
                    AppendInline(target, child, marks);
                break;

            // Unknown leaf inline → its plain text, never thrown away.
            default:
                AddText(target, PlainTextOf(inline), marks);
                break;
        }
    }

    private static void AddText(JsonArray target, string? text, HashSet<string>? marks)
    {
        if (string.IsNullOrEmpty(text))
            return;
        target.Add(Text(text, marks));
    }

    private static JsonObject Text(string text, HashSet<string>? marks = null)
    {
        var node = new JsonObject { ["type"] = "text", ["text"] = text };
        if (marks is { Count: > 0 })
        {
            var marksArray = new JsonArray();
            // Deterministic order regardless of nesting order.
            foreach (var mark in marks.OrderBy(m => m, StringComparer.Ordinal))
                marksArray.Add(new JsonObject { ["type"] = mark });
            node["marks"] = marksArray;
        }
        return node;
    }

    private static HashSet<string> With(HashSet<string>? existing, string mark)
    {
        var set = existing is null ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(existing, StringComparer.Ordinal);
        set.Add(mark);
        return set;
    }

    // Flatten an inline subtree to plain text (used for alt text, table cells, unknown inlines).
    private static string PlainTextOf(Inline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                return literal.Content.ToString();
            case CodeInline code:
                return code.Content;
            case LineBreakInline:
                return " ";
            case HtmlInline html:
                return html.Tag;
            case ContainerInline container:
                var sb = new StringBuilder();
                foreach (var child in container)
                    sb.Append(PlainTextOf(child));
                return sb.ToString();
            default:
                return string.Empty;
        }
    }

    // Flatten a block subtree (e.g. a table cell's paragraphs) to plain text.
    private static string PlainTextOfBlocks(ContainerBlock container)
    {
        var sb = new StringBuilder();
        foreach (var child in container)
        {
            switch (child)
            {
                case LeafBlock leaf when leaf.Inline is not null:
                    if (sb.Length > 0)
                        sb.Append(' ');
                    sb.Append(PlainTextOf(leaf.Inline));
                    break;
                case ContainerBlock inner:
                    var nested = PlainTextOfBlocks(inner);
                    if (nested.Length > 0)
                    {
                        if (sb.Length > 0)
                            sb.Append(' ');
                        sb.Append(nested);
                    }
                    break;
            }
        }
        return sb.ToString().Trim();
    }

    private static string RawLines(LeafBlock block) =>
        string.Join("\n", block.Lines.Lines.Take(block.Lines.Count).Select(l => l.ToString()));
}
