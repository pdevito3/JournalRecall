using System.Text;
using System.Text.Json.Nodes;
using JournalRecall.Api.Domain.Sessions.Content;

namespace JournalRecall.UnitTests.Domain.Sessions.Content;

/// <summary>
/// Unit tests for <see cref="MarkdownToProseMirror"/>: markdown parses to canonical Content JSON over
/// the supported node/mark set (paragraph, heading, lists, blockquote, codeBlock, bold/italic/code),
/// nested lists nest, unsupported constructs degrade without throwing, and the words survive a
/// markdown→JSON→plaintext walk (the round-trip the AC asks for, proven against the JSON directly
/// rather than the sibling ProseMirrorToPlainText module).
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
    public void a_link_degrades_to_its_visible_text()
    {
        var block = FirstBlock("see [the docs](https://example.com)");

        Type(block).ShouldBe("paragraph");
        PlainText(MarkdownToProseMirror.Convert("see [the docs](https://example.com)"))
            .ShouldBe("see the docs");
        // No link node, no URL leaks into text.
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
    public void a_thematic_break_is_dropped_without_throwing()
    {
        var doc = MarkdownToProseMirror.Convert("before\n\n---\n\nafter");

        // The break itself produces no node; the surrounding paragraphs remain.
        DocContent(doc).ToList().ShouldAllBe(b => Type(b!) == "paragraph");
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
