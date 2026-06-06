using JournalRecall.Api.Domain.Sessions.Content;

namespace JournalRecall.UnitTests.Domain.Sessions.Content;

/// <summary>
/// Unit tests for <see cref="ProseMirrorToPlainText"/>, the pure projection from canonical Content
/// JSON to derived plaintext: blocks become newline-separated lines, marks are stripped, mentions
/// render their label, and empty/whitespace/null/unknown input never throws.
/// </summary>
public class prose_mirror_to_plain_text_tests
{
    [Fact]
    public void a_paragraph_renders_its_text()
    {
        var json = """{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"Hello world"}]}]}""";

        ProseMirrorToPlainText.Render(json).ShouldBe("Hello world");
    }

    [Fact]
    public void blocks_are_separated_by_a_single_newline()
    {
        var json = """
        {"type":"doc","content":[
          {"type":"paragraph","content":[{"type":"text","text":"first"}]},
          {"type":"paragraph","content":[{"type":"text","text":"second"}]}
        ]}
        """;

        ProseMirrorToPlainText.Render(json).ShouldBe("first\nsecond");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void a_heading_of_any_level_renders_its_text(int level)
    {
        var json = $$"""{"type":"doc","content":[{"type":"heading","attrs":{"level":{{level}}},"content":[{"type":"text","text":"Title"}]}]}""";

        ProseMirrorToPlainText.Render(json).ShouldBe("Title");
    }

    [Theory]
    [InlineData("bold")]
    [InlineData("italic")]
    [InlineData("code")]
    public void marks_are_stripped_so_only_the_words_contribute(string mark)
    {
        var json = $$"""{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"emphasized","marks":[{"type":"{{mark}}"}]}]}]}""";

        ProseMirrorToPlainText.Render(json).ShouldBe("emphasized");
    }

    [Fact]
    public void inline_text_runs_concatenate_without_added_separators()
    {
        var json = """
        {"type":"doc","content":[{"type":"paragraph","content":[
          {"type":"text","text":"a "},
          {"type":"text","text":"bold ","marks":[{"type":"bold"}]},
          {"type":"text","text":"word"}
        ]}]}
        """;

        ProseMirrorToPlainText.Render(json).ShouldBe("a bold word");
    }

    [Fact]
    public void an_unordered_list_renders_each_item_on_its_own_line()
    {
        var json = """
        {"type":"doc","content":[{"type":"bulletList","content":[
          {"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"apples"}]}]},
          {"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"oranges"}]}]}
        ]}]}
        """;

        ProseMirrorToPlainText.Render(json).ShouldBe("apples\noranges");
    }

    [Fact]
    public void an_ordered_list_renders_each_item_text_without_numbering()
    {
        var json = """
        {"type":"doc","content":[{"type":"orderedList","attrs":{"start":1},"content":[
          {"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"step one"}]}]},
          {"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"step two"}]}]}
        ]}]}
        """;

        ProseMirrorToPlainText.Render(json).ShouldBe("step one\nstep two");
    }

    [Fact]
    public void a_nested_list_renders_inner_items_too()
    {
        var json = """
        {"type":"doc","content":[{"type":"bulletList","content":[
          {"type":"listItem","content":[
            {"type":"paragraph","content":[{"type":"text","text":"outer"}]},
            {"type":"bulletList","content":[
              {"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"inner"}]}]}
            ]}
          ]}
        ]}]}
        """;

        ProseMirrorToPlainText.Render(json).ShouldBe("outer\ninner");
    }

    [Fact]
    public void a_blockquote_renders_its_inner_blocks()
    {
        var json = """
        {"type":"doc","content":[{"type":"blockquote","content":[
          {"type":"paragraph","content":[{"type":"text","text":"quoted line"}]}
        ]}]}
        """;

        ProseMirrorToPlainText.Render(json).ShouldBe("quoted line");
    }

    [Fact]
    public void a_code_block_renders_its_text_content()
    {
        var json = """{"type":"doc","content":[{"type":"codeBlock","content":[{"type":"text","text":"var x = 1;"}]}]}""";

        ProseMirrorToPlainText.Render(json).ShouldBe("var x = 1;");
    }

    [Fact]
    public void a_mention_renders_its_label()
    {
        var json = """
        {"type":"doc","content":[{"type":"paragraph","content":[
          {"type":"text","text":"talked to "},
          {"type":"mention","attrs":{"personId":"11111111-1111-1111-1111-111111111111","label":"Sam"}},
          {"type":"text","text":" today"}
        ]}]}
        """;

        ProseMirrorToPlainText.Render(json).ShouldBe("talked to Sam today");
    }

    [Fact]
    public void an_empty_document_projects_to_empty_text()
    {
        var json = """{"type":"doc","content":[]}""";

        ProseMirrorToPlainText.Render(json).ShouldBe(string.Empty);
    }

    [Fact]
    public void a_paragraph_with_no_content_projects_to_empty_text()
    {
        var json = """{"type":"doc","content":[{"type":"paragraph"}]}""";

        ProseMirrorToPlainText.Render(json).ShouldBe(string.Empty);
    }

    [Fact]
    public void a_whitespace_only_document_projects_to_empty_text()
    {
        var json = """{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"   "}]}]}""";

        ProseMirrorToPlainText.Render(json).ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json {")]
    public void null_blank_or_invalid_json_projects_to_empty_text_without_throwing(string? json)
    {
        ProseMirrorToPlainText.Render(json).ShouldBe(string.Empty);
    }

    [Fact]
    public void a_null_node_projects_to_empty_text()
    {
        ProseMirrorToPlainText.Render((System.Text.Json.Nodes.JsonNode?)null).ShouldBe(string.Empty);
    }

    [Fact]
    public void an_unknown_node_type_recurses_into_its_content_and_does_not_throw()
    {
        var json = """
        {"type":"doc","content":[{"type":"someFutureBlock","content":[
          {"type":"paragraph","content":[{"type":"text","text":"still here"}]}
        ]}]}
        """;

        ProseMirrorToPlainText.Render(json).ShouldBe("still here");
    }

    [Fact]
    public void a_full_document_renders_every_node_kind_as_newline_separated_words()
    {
        var json = """
        {"type":"doc","content":[
          {"type":"heading","attrs":{"level":1},"content":[{"type":"text","text":"My Day"}]},
          {"type":"paragraph","content":[
            {"type":"text","text":"I felt "},
            {"type":"text","text":"great","marks":[{"type":"bold"}]},
            {"type":"text","text":" and saw "},
            {"type":"mention","attrs":{"personId":"abc","label":"Alex"}}
          ]},
          {"type":"bulletList","content":[
            {"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"walked"}]}]},
            {"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"read"}]}]}
          ]},
          {"type":"blockquote","content":[{"type":"paragraph","content":[{"type":"text","text":"a thought"}]}]},
          {"type":"codeBlock","content":[{"type":"text","text":"print(1)"}]}
        ]}
        """;

        ProseMirrorToPlainText.Render(json)
            .ShouldBe("My Day\nI felt great and saw Alex\nwalked\nread\na thought\nprint(1)");
    }
}
