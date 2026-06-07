using System.Text;
using System.Text.Json.Nodes;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace JournalRecall.Api.Domain.Sessions.Content;

/// <summary>
/// Pure, deterministic conversion from AI-emitted markdown to canonical Content
/// (ProseMirror/tiptap JSON). Per ADR-0010 the supported set is exactly what markdown can express:
/// nodes doc, paragraph, heading (levels 1-3), bulletList/orderedList/listItem, taskList/taskItem,
/// blockquote, codeBlock, horizontalRule; marks bold, italic, code, strike, underline, highlight, link.
/// (The <c>mention</c> atom is the one sanctioned non-markdown node — produced by the editor and
/// MentionInsertion, never here.) The AI is never asked to emit schema-valid editor JSON — it emits
/// markdown and the server converts here. No DB, no network.
///
/// Unsupported markdown never throws: it degrades to the nearest supported node. The degrade choices
/// are documented inline at each call site.
/// </summary>
public static class MarkdownToProseMirror
{
    // Pipeline (ADR-0010 parity contract):
    //   - Tables (pipe + grid) so a table parses as a Table block we can flatten to text.
    //   - EmphasisExtras restricted to Strikethrough | Marked | Inserted so ~~x~~ → strike, ==x== →
    //     highlight, ++x++ → underline. Subscript/superscript are deliberately NOT enabled, so a single
    //     ~tilde~ and ^carets^ stay literal text.
    //   - TaskLists so "- [ ]" / "- [x]" parse into list items carrying a TaskList inline marker.
    // Everything else stays CommonMark.
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder()
            .UsePipeTables()
            .UseGridTables()
            .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough | EmphasisExtraOptions.Marked | EmphasisExtraOptions.Inserted)
            .UseTaskLists()
            .Build();

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

            // Thematic break (---/***) → a horizontalRule node (ADR-0010 re-enables it).
            case ThematicBreakBlock:
                target.Add(new JsonObject { ["type"] = "horizontalRule" });
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
        // A list is a TASK list when its items carry task markers ("- [ ]" / "- [x]"): Markdig puts a
        // TaskList inline as the first inline of each such item's leading paragraph. We treat a list as
        // a task list when every (non-empty) item is a task item — mixed lists fall back to bullet/
        // ordered with the markers stripped from the task rows.
        var itemBlocks = list.OfType<ListItemBlock>().ToList();
        var isTaskList = itemBlocks.Count > 0 && itemBlocks.All(IsTaskItem);

        var items = new JsonArray();
        foreach (var itemBlock in itemBlocks)
        {
            if (isTaskList)
            {
                var itemContent = new JsonArray();
                foreach (var inner in itemBlock)
                    AppendBlock(itemContent, inner);
                items.Add(new JsonObject
                {
                    ["type"] = "taskItem",
                    ["attrs"] = new JsonObject { ["checked"] = TaskItemChecked(itemBlock) },
                    ["content"] = itemContent,
                });
            }
            else
            {
                var itemContent = new JsonArray();
                foreach (var inner in itemBlock)
                    AppendBlock(itemContent, inner); // paragraphs + nested lists nest inside the listItem
                items.Add(new JsonObject { ["type"] = "listItem", ["content"] = itemContent });
            }
        }

        if (isTaskList)
            return new JsonObject { ["type"] = "taskList", ["content"] = items };

        if (!list.IsOrdered)
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

    // A task item's leading paragraph starts with a Markdig TaskList inline.
    private static bool IsTaskItem(ListItemBlock item) => FindTaskMarker(item) is not null;

    private static bool TaskItemChecked(ListItemBlock item) => FindTaskMarker(item)?.Checked ?? false;

    private static TaskList? FindTaskMarker(ListItemBlock item) =>
        item.FirstOrDefault() is ParagraphBlock { Inline: { } inline }
            ? inline.FirstChild as TaskList
            : null;

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

    // marks: the active mark set inherited from enclosing emphasis/code/link wrappers.
    private static void AppendInline(JsonArray target, Inline inline, MarkSet? marks)
    {
        switch (inline)
        {
            // The TaskList marker inline ("[ ]"/"[x]") is structural, not content — never render it as
            // literal text; the checked state is read off the item in List().
            case TaskList:
                break;

            case LiteralInline literal:
                AddText(target, literal.Content.ToString(), marks);
                break;

            // A soft/hard line break inside a paragraph becomes a space so words don't run together.
            case LineBreakInline:
                AddText(target, " ", marks);
                break;

            case CodeInline code:
                AddText(target, code.Content, MarkSet.With(marks, Mark.Of("code")));
                break;

            case EmphasisInline emphasis:
            {
                // Branch on the delimiter CHARACTER, not only the count, so the EmphasisExtras
                // delimiters carry their own marks (Markdig 0.42):
                //   * / _  → 2+ delimiters = bold, 1 = italic   (CommonMark emphasis)
                //   ~      → strikethrough (DelimiterChar '~', count 2)  → strike
                //   =      → marked        (DelimiterChar '=', count 2)  → highlight
                //   +      → inserted      (DelimiterChar '+', count 2)  → underline
                // An unrecognized delimiter char carries NO mark — we just recurse the children as
                // plain text so its words survive (this is the safe degrade for any future extra).
                Mark? mark = emphasis.DelimiterChar switch
                {
                    '*' or '_' => Mark.Of(emphasis.DelimiterCount >= 2 ? "bold" : "italic"),
                    '~' => Mark.Of("strike"),
                    '=' => Mark.Of("highlight"),
                    '+' => Mark.Of("underline"),
                    _ => null,
                };

                var nested = mark is null ? marks : MarkSet.With(marks, mark);
                foreach (var child in emphasis)
                    AppendInline(target, child, nested);
                break;
            }

            // Links emit a link mark on their visible text (matching tiptap's Link default attrs);
            // images degrade to their alt text (no image node in the set). Either way the words survive.
            case LinkInline link:
            {
                if (link.IsImage)
                {
                    AddText(target, PlainTextOf(link), marks); // alt text (the link's children)
                }
                else
                {
                    var nested = MarkSet.With(marks, Mark.Link(link.Url));
                    foreach (var child in link)
                        AppendInline(target, child, nested);
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

    private static void AddText(JsonArray target, string? text, MarkSet? marks)
    {
        if (string.IsNullOrEmpty(text))
            return;
        target.Add(Text(text, marks));
    }

    private static JsonObject Text(string text, MarkSet? marks = null)
    {
        var node = new JsonObject { ["type"] = "text", ["text"] = text };
        var marksArray = marks?.ToJsonArray();
        if (marksArray is { Count: > 0 })
            node["marks"] = marksArray;
        return node;
    }

    // Flatten an inline subtree to plain text (used for alt text, table cells, unknown inlines).
    private static string PlainTextOf(Inline inline)
    {
        switch (inline)
        {
            case TaskList:
                return string.Empty; // the "[ ]"/"[x]" marker is structural, not words
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

    // ---- mark model ---------------------------------------------------------------------------

    /// <summary>
    /// A single active mark. Most marks are attr-less (bold/italic/code/strike/underline/highlight) and
    /// are uniquely identified by <see cref="Type"/>; <c>link</c> additionally carries attrs (href +
    /// tiptap's default target/rel). Modeling attrs here lets the mark-emission path stay uniform.
    /// </summary>
    private sealed record Mark(string Type, JsonObject? Attrs = null)
    {
        public static Mark Of(string type) => new(type);

        // Match tiptap's Link extension default attrs (href, target, rel) so the human-editor and
        // AI-cleanup write paths produce identical link shapes. tiptap also emits a null `class`; we
        // omit it (a null attr carries no information and keeps the JSON tidy).
        public static Mark Link(string? href) => new(
            "link",
            new JsonObject
            {
                ["href"] = href ?? string.Empty,
                ["target"] = "_blank",
                ["rel"] = "noopener noreferrer nofollow",
            });

        public JsonObject ToJson()
        {
            var node = new JsonObject { ["type"] = Type };
            if (Attrs is not null)
                node["attrs"] = (JsonObject)Attrs.DeepClone();
            return node;
        }
    }

    /// <summary>
    /// An immutable, deterministically-ordered set of active marks. Adding the same mark <c>Type</c>
    /// twice keeps the first (so nested identical wrappers don't duplicate); emission sorts by mark
    /// type with <see cref="StringComparer.Ordinal"/> so output is stable regardless of nesting order.
    /// </summary>
    private sealed class MarkSet
    {
        private readonly List<Mark> _marks;

        private MarkSet(List<Mark> marks) => _marks = marks;

        public static MarkSet With(MarkSet? existing, Mark mark)
        {
            var marks = existing is null ? new List<Mark>() : new List<Mark>(existing._marks);
            if (marks.All(m => m.Type != mark.Type))
                marks.Add(mark);
            return new MarkSet(marks);
        }

        public JsonArray ToJsonArray()
        {
            var array = new JsonArray();
            foreach (var mark in _marks.OrderBy(m => m.Type, StringComparer.Ordinal))
                array.Add(mark.ToJson());
            return array;
        }
    }
}
