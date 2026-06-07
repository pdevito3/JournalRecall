using JournalRecall.Api.Domain.Sessions.Content;
using Shouldly;

namespace JournalRecall.UnitTests.Domain.Sessions.Content;

// Cross-module proof of the RICH-002 acceptance criterion: markdown -> canonical JSON
// (MarkdownToProseMirror) -> plaintext (ProseMirrorToPlainText) preserves the words, because both
// modules speak the same canonical ProseMirror/tiptap schema.
public class markdown_round_trip_tests
{
    private static string RoundTrip(string markdown) =>
        ProseMirrorToPlainText.Render(MarkdownToProseMirror.Convert(markdown));

    [Fact]
    public void words_survive_a_rich_document_round_trip()
    {
        const string markdown = """
            # My day

            I felt **content** and *a little tired*. Here is `code`.

            - first thing
            - second thing
              - nested thing

            1. step one
            2. step two

            > a quote worth keeping

            ```
            literal block
            ```
            """;

        var text = RoundTrip(markdown);

        foreach (var word in new[]
        {
            "My", "day", "content", "little", "tired", "code", "first", "thing", "second",
            "nested", "step", "one", "two", "quote", "worth", "keeping", "literal", "block",
        })
            text.ShouldContain(word);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void empty_markdown_round_trips_to_empty_text(string markdown) =>
        RoundTrip(markdown).Trim().ShouldBeEmpty();

    [Fact]
    public void marks_are_dropped_but_their_text_is_kept()
    {
        var text = RoundTrip("This is **bold** and *italic* and `mono`.");

        text.ShouldNotContain("*");
        text.ShouldNotContain("`");
        text.ShouldContain("bold");
        text.ShouldContain("italic");
        text.ShouldContain("mono");
    }

    [Fact]
    public void expanded_marks_drop_their_delimiters_but_keep_the_words()
    {
        var text = RoundTrip("~~struck~~ and ==marked== and ++under++ and a [link](https://x.com).");

        text.ShouldNotContain("~");
        text.ShouldNotContain("=");
        text.ShouldNotContain("+");
        text.ShouldNotContain("https://x.com"); // the URL lives in the link mark, not the words
        foreach (var word in new[] { "struck", "marked", "under", "link" })
            text.ShouldContain(word);
    }

    [Fact]
    public void task_lists_and_rules_round_trip_to_their_words_only()
    {
        const string markdown = """
            - [ ] open task
            - [x] closed task

            ---

            after the rule
            """;

        var text = RoundTrip(markdown);

        text.ShouldNotContain("["); // no checkbox glyph
        text.ShouldContain("open task");
        text.ShouldContain("closed task");
        text.ShouldContain("after the rule");
        // The horizontalRule contributes no blank line.
        text.ShouldNotContain("\n\n");
    }
}
