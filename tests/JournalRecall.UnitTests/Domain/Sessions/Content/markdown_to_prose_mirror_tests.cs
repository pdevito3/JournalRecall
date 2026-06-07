using System.Text;
using System.Text.Json.Nodes;
using JournalRecall.Api.Domain.Sessions.Content;

namespace JournalRecall.UnitTests.Domain.Sessions.Content;

/// <summary>
/// Unit tests for <see cref="MarkdownToProseMirror"/>: markdown parses to canonical Content JSON over
/// the ADR-0010 supported node/mark set (paragraph, heading, bullet/ordered/task lists, blockquote,
/// codeBlock, horizontalRule; bold/italic/code/strike/underline/highlight/link), nested lists nest,
/// unsupported constructs degrade without throwing, and the words survive a markdown→JSON→plaintext
/// walk (the round-trip the AC asks for, proven against the JSON directly rather than the sibling
/// ProseMirrorToPlainText module).
/// </summary>
public class markdown_to_prose_mirror_tests
{
    // ---- local helpers ------------------------------------------------------------------------

    /// <summary>Walks the doc node and concatenates every "text" value, joining sibling blocks with a
    /// single space — a tiny stand-in for the sibling plaintext extractor, used only to prove word
    /// preservation. Inline text within a block is concatenated as-is (the module preserves the
    /// intra-block spacing itself), and surrounding whitespace is collapsed.</summary>
    private static string PlainText(JsonNode doc)
    {
        var blocks = new List<string>();
        WalkBlocks(doc);
        return string.Join(" ", blocks.Where(b => b.Length > 0));

        void WalkBlocks(JsonNode? node)
        {
            if (node is not JsonObject obj)
                return;

            if (obj["type"]?.GetValue<string>() is "text")
                return;

            // A leaf-text-bearing block: collect its inline text, then recurse for nested blocks.
            if (obj["content"] is JsonArray content)
            {
                var sb = new StringBuilder();
                foreach (var child in content)
                {
                    if (child is JsonObject c && c["type"]?.GetValue<string>() == "text" && c["text"] is JsonValue v)
                        sb.Append(v.GetValue<string>());
                    else
                        WalkBlocks(child);
                }
                var inline = sb.ToString().Trim();
                if (inline.Length > 0)
                    blocks.Add(inline);
            }
        }
    }

    private static JsonArray DocContent(JsonNode doc) => doc["content"]!.AsArray();

    private static JsonNode FirstBlock(string markdown) => DocContent(MarkdownToProseMirror.Convert(markdown))[0]!;

    private static string Type(JsonNode node) => node["type"]!.GetValue<string>();

    private static string[] MarksOf(JsonNode textNode) =>
        textNode["marks"] is JsonArray a
            ? a.Select(m => m!["type"]!.GetValue<string>()).ToArray()
            : Array.Empty<string>();

    /// <summary>The attrs object of the named mark on a text node, or null if the mark is absent.</summary>
    private static JsonObject? MarkAttrs(JsonNode textNode, string markType) =>
        textNode["marks"] is JsonArray a
            ? a.FirstOrDefault(m => m!["type"]!.GetValue<string>() == markType)?["attrs"] as JsonObject
            : null;

    // ---- empty / degrade-to-empty -------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t \n")]
    public void empty_or_whitespace_markdown_is_an_empty_doc(string? markdown)
    {
        var doc = MarkdownToProseMirror.Convert(markdown);

        Type(doc).ShouldBe("doc");
        DocContent(doc).Count.ShouldBe(0);
    }

    [Fact]
    public void convert_never_returns_null()
    {
        MarkdownToProseMirror.Convert(null).ShouldNotBeNull();
    }

    [Fact]
    public void convert_to_json_emits_a_doc_root()
    {
        MarkdownToProseMirror.ConvertToJson("hi").ShouldStartWith("{\"type\":\"doc\"");
    }

    // ---- paragraphs & inline marks ------------------------------------------------------------

    [Fact]
    public void a_plain_line_is_a_paragraph_with_one_text_node()
    {
        var block = FirstBlock("just some words");

        Type(block).ShouldBe("paragraph");
        var text = block["content"]![0]!;
        text["text"]!.GetValue<string>().ShouldBe("just some words");
        MarksOf(text).ShouldBeEmpty();
    }

    [Fact]
    public void bold_with_double_star_becomes_a_bold_mark()
    {
        var block = FirstBlock("**strong**");
        MarksOf(block["content"]![0]!).ShouldBe(new[] { "bold" });
    }

    [Fact]
    public void bold_with_double_underscore_becomes_a_bold_mark()
    {
        var block = FirstBlock("__strong__");
        MarksOf(block["content"]![0]!).ShouldBe(new[] { "bold" });
    }

    [Fact]
    public void italic_with_single_star_becomes_an_italic_mark()
    {
        var block = FirstBlock("*soft*");
        MarksOf(block["content"]![0]!).ShouldBe(new[] { "italic" });
    }

    [Fact]
    public void italic_with_single_underscore_becomes_an_italic_mark()
    {
        var block = FirstBlock("_soft_");
        MarksOf(block["content"]![0]!).ShouldBe(new[] { "italic" });
    }

    [Fact]
    public void inline_backticks_become_a_code_mark()
    {
        var block = FirstBlock("call `Foo()` now");

        var codeNode = block["content"]!.AsArray()
            .First(n => n!["text"]!.GetValue<string>() == "Foo()")!;
        MarksOf(codeNode).ShouldBe(new[] { "code" });
    }

    [Fact]
    public void nested_emphasis_carries_both_marks()
    {
        var block = FirstBlock("***both***");
        MarksOf(block["content"]![0]!).ShouldBe(new[] { "bold", "italic" });
    }

    [Fact]
    public void a_paragraph_mixes_marked_and_unmarked_runs()
    {
        var block = FirstBlock("plain **bold** plain");
        var runs = block["content"]!.AsArray();

        runs.Count.ShouldBeGreaterThanOrEqualTo(2);
        runs.Any(r => r!["text"]!.GetValue<string>() == "bold" && MarksOf(r).Contains("bold")).ShouldBeTrue();
        runs.Any(r => r!["text"]!.GetValue<string>().Contains("plain") && MarksOf(r).Length == 0).ShouldBeTrue();
    }

    // ---- headings -----------------------------------------------------------------------------

    [Theory]
    [InlineData("# h", 1)]
    [InlineData("## h", 2)]
    [InlineData("### h", 3)]
    public void atx_headings_map_to_their_level(string markdown, int level)
    {
        var block = FirstBlock(markdown);

        Type(block).ShouldBe("heading");
        block["attrs"]!["level"]!.GetValue<int>().ShouldBe(level);
    }

    [Theory]
    [InlineData("#### h")]
    [InlineData("##### h")]
    [InlineData("###### h")]
    public void deep_headings_clamp_to_level_three(string markdown)
    {
        FirstBlock(markdown)["attrs"]!["level"]!.GetValue<int>().ShouldBe(3);
    }

    [Fact]
    public void setext_heading_maps_to_a_heading()
    {
        var block = FirstBlock("Title\n=====");

        Type(block).ShouldBe("heading");
        block["attrs"]!["level"]!.GetValue<int>().ShouldBe(1);
    }

    // ---- lists --------------------------------------------------------------------------------

    [Theory]
    [InlineData("- one\n- two")]
    [InlineData("* one\n* two")]
    [InlineData("+ one\n+ two")]
    public void dash_star_plus_make_a_bullet_list(string markdown)
    {
        var block = FirstBlock(markdown);

        Type(block).ShouldBe("bulletList");
        var items = block["content"]!.AsArray();
        items.Count.ShouldBe(2);
        Type(items[0]!).ShouldBe("listItem");
        Type(items[0]!["content"]![0]!).ShouldBe("paragraph");
    }

    [Fact]
    public void numbered_list_makes_an_ordered_list_starting_at_one()
    {
        var block = FirstBlock("1. one\n2. two");

        Type(block).ShouldBe("orderedList");
        block["attrs"]!["start"]!.GetValue<int>().ShouldBe(1);
        block["content"]!.AsArray().Count.ShouldBe(2);
    }

    [Fact]
    public void ordered_list_carries_a_nonstandard_start()
    {
        var block = FirstBlock("3. three\n4. four");
        block["attrs"]!["start"]!.GetValue<int>().ShouldBe(3);
    }

    [Fact]
    public void nested_lists_nest_inside_the_parent_list_item()
    {
        var doc = MarkdownToProseMirror.Convert("- outer\n    - inner");
        var outerList = DocContent(doc)[0]!;

        Type(outerList).ShouldBe("bulletList");
        var firstItem = outerList["content"]![0]!;
        Type(firstItem).ShouldBe("listItem");

        var itemChildren = firstItem["content"]!.AsArray();
        // listItem holds a paragraph ("outer") then the nested bulletList.
        Type(itemChildren[0]!).ShouldBe("paragraph");
        var nested = itemChildren.First(c => Type(c!) == "bulletList")!;
        nested["content"]![0]!["content"]![0]!["content"]![0]!["text"]!.GetValue<string>().ShouldBe("inner");
    }

    // ---- blockquote ---------------------------------------------------------------------------

    [Fact]
    public void blockquote_wraps_its_paragraph()
    {
        var block = FirstBlock("> quoted line");

        Type(block).ShouldBe("blockquote");
        Type(block["content"]![0]!).ShouldBe("paragraph");
        block["content"]![0]!["content"]![0]!["text"]!.GetValue<string>().ShouldBe("quoted line");
    }

    // ---- code blocks --------------------------------------------------------------------------

    [Fact]
    public void fenced_code_block_becomes_a_code_block_node()
    {
        var block = FirstBlock("```\nvar x = 1;\nvar y = 2;\n```");

        Type(block).ShouldBe("codeBlock");
        var text = block["content"]![0]!;
        Type(text).ShouldBe("text");
        text["text"]!.GetValue<string>().ShouldBe("var x = 1;\nvar y = 2;");
    }

    [Fact]
    public void indented_code_block_becomes_a_code_block_node()
    {
        var block = FirstBlock("    indented code");

        Type(block).ShouldBe("codeBlock");
        block["content"]![0]!["text"]!.GetValue<string>().ShouldBe("indented code");
    }

    // ---- graceful degradation -----------------------------------------------------------------

    [Fact]
    public void a_link_keeps_its_visible_text_as_words()
    {
        var block = FirstBlock("see [the docs](https://example.com)");

        Type(block).ShouldBe("paragraph");
        PlainText(MarkdownToProseMirror.Convert("see [the docs](https://example.com)"))
            .ShouldBe("see the docs");
        // The URL lives in the mark attrs, never leaking into any text node.
        block["content"]!.AsArray().Any(n => n!["text"]!.GetValue<string>().Contains("example.com"))
            .ShouldBeFalse();
    }

    [Fact]
    public void an_image_degrades_to_its_alt_text()
    {
        var doc = MarkdownToProseMirror.Convert("![a sunset photo](pic.png)");
        PlainText(doc).ShouldBe("a sunset photo");
        PlainText(doc).ShouldNotContain("pic.png");
    }

    [Fact]
    public void a_table_degrades_to_paragraphs_preserving_the_words()
    {
        const string md = "| A | B |\n|---|---|\n| one | two |\n| three | four |";
        var doc = MarkdownToProseMirror.Convert(md);

        DocContent(doc).Count.ShouldBeGreaterThan(0);
        DocContent(doc).ToList().ShouldAllBe(b => Type(b!) == "paragraph");
        var plain = PlainText(doc);
        plain.ShouldContain("one");
        plain.ShouldContain("four");
    }

    [Fact]
    public void a_thematic_break_emits_a_horizontal_rule_between_the_paragraphs()
    {
        var doc = MarkdownToProseMirror.Convert("before\n\n---\n\nafter");

        var blocks = DocContent(doc);
        Type(blocks[0]!).ShouldBe("paragraph");
        Type(blocks[1]!).ShouldBe("horizontalRule");
        Type(blocks[2]!).ShouldBe("paragraph");
        // The rule carries no content/attrs; the surrounding words survive.
        PlainText(doc).ShouldBe("before after");
    }

    [Fact]
    public void raw_html_does_not_throw_and_keeps_its_words()
    {
        var doc = MarkdownToProseMirror.Convert("<div>raw block</div>");
        PlainText(doc).ShouldContain("raw block");
    }

    [Fact]
    public void inline_html_keeps_its_words()
    {
        var doc = MarkdownToProseMirror.Convert("a <span>tagged</span> word");
        PlainText(doc).ShouldContain("tagged");
    }

    // ---- expanded marks: strike / highlight / underline (ADR-0010) ----------------------------

    [Fact]
    public void double_tilde_becomes_a_strike_mark()
    {
        var block = FirstBlock("~~struck~~");
        MarksOf(block["content"]![0]!).ShouldBe(new[] { "strike" });
        block["content"]![0]!["text"]!.GetValue<string>().ShouldBe("struck");
    }

    [Fact]
    public void double_equals_becomes_a_highlight_mark()
    {
        var block = FirstBlock("==marked==");
        MarksOf(block["content"]![0]!).ShouldBe(new[] { "highlight" });
        block["content"]![0]!["text"]!.GetValue<string>().ShouldBe("marked");
    }

    [Fact]
    public void double_plus_becomes_an_underline_mark()
    {
        var block = FirstBlock("++inserted++");
        MarksOf(block["content"]![0]!).ShouldBe(new[] { "underline" });
        block["content"]![0]!["text"]!.GetValue<string>().ShouldBe("inserted");
    }

    [Fact]
    public void expanded_marks_combine_with_inherited_marks_in_deterministic_order()
    {
        // **bold _italic ~~strike~~_** — the innermost run carries all three, sorted Ordinal.
        var block = FirstBlock("**bold _italic ~~strike~~_**");
        var struck = block["content"]!.AsArray().First(n => n!["text"]!.GetValue<string>() == "strike")!;
        MarksOf(struck).ShouldBe(new[] { "bold", "italic", "strike" });
    }

    // Subscript/superscript are NOT enabled — these delimiters stay literal text (ADR-0010).
    [Theory]
    [InlineData("~single~", "~single~")]
    [InlineData("^carets^", "^carets^")]
    [InlineData("=one=", "=one=")]
    [InlineData("+one+", "+one+")]
    public void unenabled_single_delimiters_stay_literal_text(string markdown, string expected)
    {
        var block = FirstBlock(markdown);
        Type(block).ShouldBe("paragraph");
        block["content"]![0]!["text"]!.GetValue<string>().ShouldBe(expected);
        MarksOf(block["content"]![0]!).ShouldBeEmpty();
    }

    // ---- links (link mark with tiptap default attrs) ------------------------------------------

    [Fact]
    public void a_link_emits_a_link_mark_with_tiptap_default_attrs()
    {
        var block = FirstBlock("[the docs](https://example.com)");
        var run = block["content"]![0]!;

        run["text"]!.GetValue<string>().ShouldBe("the docs");
        MarksOf(run).ShouldBe(new[] { "link" });

        var attrs = MarkAttrs(run, "link")!;
        attrs["href"]!.GetValue<string>().ShouldBe("https://example.com");
        attrs["target"]!.GetValue<string>().ShouldBe("_blank");
        attrs["rel"]!.GetValue<string>().ShouldBe("noopener noreferrer nofollow");
    }

    [Fact]
    public void a_link_mark_combines_with_inherited_marks()
    {
        // Bold wrapping a link: the visible text carries both bold and link (sorted Ordinal).
        var block = FirstBlock("**see [docs](https://example.com)**");
        var run = block["content"]!.AsArray().First(n => n!["text"]!.GetValue<string>() == "docs")!;
        MarksOf(run).ShouldBe(new[] { "bold", "link" });
        MarkAttrs(run, "link")!["href"]!.GetValue<string>().ShouldBe("https://example.com");
    }

    [Fact]
    public void an_image_still_degrades_to_its_alt_text_with_no_link_mark()
    {
        var block = FirstBlock("![a sunset photo](pic.png)");
        var run = block["content"]![0]!;
        run["text"]!.GetValue<string>().ShouldBe("a sunset photo");
        MarksOf(run).ShouldBeEmpty();
        block["content"]!.AsArray().Any(n => n!["text"]!.GetValue<string>().Contains("pic.png")).ShouldBeFalse();
    }

    // ---- horizontal rule ----------------------------------------------------------------------

    [Theory]
    [InlineData("---")]
    [InlineData("***")]
    [InlineData("___")]
    public void a_thematic_break_emits_a_bare_horizontal_rule_node(string markdown)
    {
        var block = FirstBlock(markdown);
        Type(block).ShouldBe("horizontalRule");
        // A bare atom: no content, no attrs.
        block.AsObject().ContainsKey("content").ShouldBeFalse();
        block.AsObject().ContainsKey("attrs").ShouldBeFalse();
    }

    // ---- task lists ---------------------------------------------------------------------------

    [Fact]
    public void task_list_items_become_task_list_and_task_items_with_checked_state()
    {
        var block = FirstBlock("- [ ] todo\n- [x] done");

        Type(block).ShouldBe("taskList");
        var items = block["content"]!.AsArray();
        items.Count.ShouldBe(2);

        Type(items[0]!).ShouldBe("taskItem");
        items[0]!["attrs"]!["checked"]!.GetValue<bool>().ShouldBeFalse();
        Type(items[1]!).ShouldBe("taskItem");
        items[1]!["attrs"]!["checked"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public void a_task_item_wraps_its_text_in_a_paragraph_without_the_marker_glyph()
    {
        var block = FirstBlock("- [x] buy milk");

        var item = block["content"]![0]!;
        Type(item["content"]![0]!).ShouldBe("paragraph");
        // The "[x]" marker is stripped — only the words (and Markdig's trailing space) survive.
        item["content"]![0]!["content"]![0]!["text"]!.GetValue<string>().Trim().ShouldBe("buy milk");
        // No literal "[x]"/"[ ]" leaks anywhere.
        PlainText(MarkdownToProseMirror.Convert("- [x] buy milk")).ShouldNotContain("[");
    }

    [Fact]
    public void capital_X_marks_a_task_item_checked()
    {
        var block = FirstBlock("- [X] done");
        block["content"]![0]!["attrs"]!["checked"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public void a_mixed_list_degrades_to_a_bullet_list_when_not_every_item_is_a_task()
    {
        // Only some items are tasks → fall back to bulletList; the task marker is still stripped.
        var block = FirstBlock("- [ ] todo\n- plain");

        Type(block).ShouldBe("bulletList");
        var items = block["content"]!.AsArray();
        items.Count.ShouldBe(2);
        items.ToList().ShouldAllBe(i => Type(i!) == "listItem");
        PlainText(MarkdownToProseMirror.Convert("- [ ] todo\n- plain")).ShouldNotContain("[");
    }

    // ---- parity invariant (ADR-0010) ----------------------------------------------------------

    [Fact]
    public void every_adr0010_node_and_mark_except_mention_is_producible_from_markdown()
    {
        // One document exercising every supported shape; assert each node type and mark appears.
        const string md = """
            # h1
            ## h2
            ### h3

            A paragraph with **bold**, *italic*, `code`, ~~strike~~, ==highlight==, ++underline++,
            and a [link](https://example.com).

            > a blockquote

            - bullet one
            - bullet two

            1. ordered one
            2. ordered two

            - [ ] task open
            - [x] task done

            ```
            code block
            ```

            ---
            """;

        var doc = MarkdownToProseMirror.Convert(md);

        var nodeTypes = new HashSet<string>();
        var markTypes = new HashSet<string>();
        Collect(doc);

        foreach (var node in new[]
                 {
                     "doc", "paragraph", "heading", "bulletList", "orderedList", "listItem",
                     "taskList", "taskItem", "blockquote", "codeBlock", "horizontalRule",
                 })
            nodeTypes.ShouldContain(node);

        foreach (var mark in new[] { "bold", "italic", "code", "strike", "underline", "highlight", "link" })
            markTypes.ShouldContain(mark);

        // heading levels 1-3 all reachable.
        var headingLevels = new HashSet<int>();
        CollectHeadingLevels(doc);
        headingLevels.ShouldBe(new HashSet<int> { 1, 2, 3 }, ignoreOrder: true);

        void Collect(JsonNode? node)
        {
            if (node is not JsonObject obj)
                return;
            if (obj["type"]?.GetValue<string>() is { } t)
                nodeTypes.Add(t);
            if (obj["marks"] is JsonArray marks)
                foreach (var m in marks)
                    markTypes.Add(m!["type"]!.GetValue<string>());
            if (obj["content"] is JsonArray content)
                foreach (var c in content)
                    Collect(c);
        }

        void CollectHeadingLevels(JsonNode? node)
        {
            if (node is not JsonObject obj)
                return;
            if (obj["type"]?.GetValue<string>() == "heading")
                headingLevels.Add(obj["attrs"]!["level"]!.GetValue<int>());
            if (obj["content"] is JsonArray content)
                foreach (var c in content)
                    CollectHeadingLevels(c);
        }
    }

    // ---- round-trip word preservation (the AC) ------------------------------------------------

    [Fact]
    public void a_rich_document_preserves_all_its_words_through_the_walk()
    {
        const string md = """
            # My Day

            Today was **great** and a little *tired*. I wrote some `code`.

            > A quote worth keeping.

            - first thing
            - second thing
                - nested detail

            1. step one
            2. step two

            ```
            run();
            ```
            """;

        var plain = PlainText(MarkdownToProseMirror.Convert(md));

        foreach (var word in new[]
                 {
                     "My", "Day", "Today", "great", "tired", "code", "quote", "keeping",
                     "first", "second", "nested", "detail", "step", "one", "two", "run();",
                 })
            plain.ShouldContain(word);
    }

    [Fact]
    public void conversion_is_deterministic()
    {
        const string md = "## Heading\n\n- a **bold** item\n- a *soft* item";
        MarkdownToProseMirror.ConvertToJson(md).ShouldBe(MarkdownToProseMirror.ConvertToJson(md));
    }
}
