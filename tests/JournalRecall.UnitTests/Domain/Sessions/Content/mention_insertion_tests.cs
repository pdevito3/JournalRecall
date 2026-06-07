using System.Text.Json.Nodes;
using JournalRecall.Api.Domain.Sessions.Content;

namespace JournalRecall.UnitTests.Domain.Sessions.Content;

/// <summary>
/// Unit tests for <see cref="MentionInsertion"/> (PRD-0006, RICH-008): the pure transform wraps approved
/// plaintext-offset spans in <c>mention</c> nodes, preserves surrounding text and marks, keeps offsets
/// consistent across multiple insertions, and skips stale/mismatched/cross-boundary spans without
/// corrupting the document. No host or DB.
/// </summary>
public class mention_insertion_tests
{
    // ---- helpers ------------------------------------------------------------------------------

    /// <summary>A doc with a single paragraph holding the given inline nodes.</summary>
    private static JsonNode Para(params JsonObject[] inline) => Doc(Paragraph(inline));

    /// <summary>A doc with the given block nodes.</summary>
    private static JsonNode Doc(params JsonObject[] blocks) => new JsonObject
    {
        ["type"] = "doc",
        ["content"] = Arr(blocks),
    };

    private static JsonObject Paragraph(params JsonObject[] inline) =>
        new() { ["type"] = "paragraph", ["content"] = Arr(inline) };

    private static JsonObject Text(string text, params string[] marks)
    {
        var node = new JsonObject { ["type"] = "text", ["text"] = text };
        if (marks.Length > 0)
        {
            var markArr = new JsonArray();
            foreach (var m in marks)
                markArr.Add(new JsonObject { ["type"] = m });
            node["marks"] = markArr;
        }
        return node;
    }

    private static JsonArray Arr(JsonObject[] items)
    {
        var array = new JsonArray();
        foreach (var item in items)
            array.Add(item);
        return array;
    }

    /// <summary>The mention nodes in document order, as (personId, label) pairs.</summary>
    private static List<(Guid PersonId, string Label)> Mentions(JsonNode? doc)
    {
        var found = new List<(Guid, string)>();
        Walk(doc);
        return found;

        void Walk(JsonNode? node)
        {
            if (node is not JsonObject obj) return;
            if ((obj["type"] as JsonValue)?.GetValue<string>() == "mention" && obj["attrs"] is JsonObject a)
                found.Add((Guid.Parse(a["personId"]!.GetValue<string>()), a["label"]!.GetValue<string>()));
            if (obj["content"] is JsonArray arr)
                foreach (var c in arr) Walk(c);
        }
    }

    // ---- tracer -------------------------------------------------------------------------------

    /// <summary>The paragraph's inline children as (type, text-or-label, marks) for structural assertions.
    /// Marks are joined to a string so tuple equality compares them by value.</summary>
    private static List<(string Type, string Value, string Marks)> Inline(JsonNode? doc)
    {
        var para = doc!["content"]!.AsArray()[0]!["content"]!.AsArray();
        var result = new List<(string, string, string)>();
        foreach (var n in para)
        {
            var obj = n!.AsObject();
            var type = obj["type"]!.GetValue<string>();
            var value = type == "mention"
                ? obj["attrs"]!["label"]!.GetValue<string>()
                : obj["text"]!.GetValue<string>();
            var marks = string.Join("+", obj["marks"]?.AsArray().Select(m => m!["type"]!.GetValue<string>()) ?? []);
            result.Add((type, value, marks));
        }
        return result;
    }

    // ---- tracer -------------------------------------------------------------------------------

    [Fact]
    public void wraps_a_single_span_in_a_mention_node()
    {
        var sam = Guid.CreateVersion7();
        var doc = Para(Text("Lunch with Sam today")); // plaintext: "Lunch with Sam today"

        var result = MentionInsertion.Insert(doc, [new MentionSpan(11, "Sam", sam, "Sam")]);

        Mentions(result).ShouldBe([(sam, "Sam")]);
        // The words are unchanged — a mention renders its label, so the plaintext round-trips.
        ProseMirrorToPlainText.Render(result).ShouldBe("Lunch with Sam today");
    }

    [Fact]
    public void splits_surrounding_text_and_preserves_its_marks()
    {
        var sam = Guid.CreateVersion7();
        var doc = Para(Text("Met Sam there", "bold")); // one bold run: "Met Sam there"

        var result = MentionInsertion.Insert(doc, [new MentionSpan(4, "Sam", sam, "Sam")]);

        Inline(result).ShouldBe(
        [
            ("text", "Met ", "bold"),
            ("mention", "Sam", ""),
            ("text", " there", "bold"),
        ]);
    }

    [Fact]
    public void inserts_multiple_spans_in_one_block_with_consistent_offsets()
    {
        var sam = Guid.CreateVersion7();
        var alex = Guid.CreateVersion7();
        var doc = Para(Text("Sam and Alex hiked")); // "Alex" begins at offset 8, unshifted by wrapping "Sam"

        var result = MentionInsertion.Insert(doc,
            [new MentionSpan(0, "Sam", sam, "Sam"), new MentionSpan(8, "Alex", alex, "Alex")]);

        Inline(result).ShouldBe(
        [
            ("mention", "Sam", ""),
            ("text", " and ", ""),
            ("mention", "Alex", ""),
            ("text", " hiked", ""),
        ]);
        ProseMirrorToPlainText.Render(result).ShouldBe("Sam and Alex hiked");
    }

    [Fact]
    public void resolves_offsets_across_block_separators()
    {
        var sam = Guid.CreateVersion7();
        var alex = Guid.CreateVersion7();
        // Two paragraphs join with '\n': "Sam called\nMet Alex" — Sam at 0, Alex at 15 (past the separator).
        var doc = Doc(Paragraph(Text("Sam called")), Paragraph(Text("Met Alex")));

        var result = MentionInsertion.Insert(doc,
            [new MentionSpan(0, "Sam", sam, "Sam"), new MentionSpan(15, "Alex", alex, "Alex")]);

        Mentions(result).ShouldBe([(sam, "Sam"), (alex, "Alex")]);
        ProseMirrorToPlainText.Render(result).ShouldBe("Sam called\nMet Alex");
    }

    [Fact]
    public void skips_stale_and_out_of_range_spans_but_applies_the_valid_one()
    {
        var sam = Guid.CreateVersion7();
        var ghost = Guid.CreateVersion7();
        var doc = Para(Text("Lunch with Sam today"));

        var result = MentionInsertion.Insert(doc,
        [
            new MentionSpan(11, "Sam", sam, "Sam"),     // valid
            new MentionSpan(0, "Sam", ghost, "Sam"),    // stale: offset 0 is "Lunch", not "Sam"
            new MentionSpan(100, "Sam", ghost, "Sam"),  // out of range
        ]);

        Mentions(result).ShouldBe([(sam, "Sam")]); // only the valid span applied; doc not corrupted
        ProseMirrorToPlainText.Render(result).ShouldBe("Lunch with Sam today");
    }

    [Fact]
    public void skips_a_span_that_straddles_a_block_boundary()
    {
        var ghost = Guid.CreateVersion7();
        var doc = Doc(Paragraph(Text("Sam called")), Paragraph(Text("Met Alex")));

        // "called\nMet" crosses the '\n' separator between two text nodes — not a single run.
        var result = MentionInsertion.Insert(doc, [new MentionSpan(4, "called\nMet", ghost, "X")]);

        Mentions(result).ShouldBeEmpty();
        ProseMirrorToPlainText.Render(result).ShouldBe("Sam called\nMet Alex");
    }

    [Fact]
    public void will_not_wrap_inside_an_existing_mention()
    {
        var sam = Guid.CreateVersion7();
        var ghost = Guid.CreateVersion7();
        // Paragraph: text "Hi " + an existing mention labeled "Sam" → plaintext "Hi Sam".
        var existing = new JsonObject
        {
            ["type"] = "mention",
            ["attrs"] = new JsonObject { ["personId"] = sam.ToString(), ["label"] = "Sam" },
        };
        var doc = Para(Text("Hi "), existing);

        var result = MentionInsertion.Insert(doc, [new MentionSpan(3, "Sam", ghost, "Sam")]);

        Mentions(result).ShouldBe([(sam, "Sam")]); // the original mention only; the span over it is skipped
    }

    [Fact]
    public void overlapping_approved_spans_keep_the_first_and_skip_the_rest()
    {
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        var doc = Para(Text("Sam Sam")); // "Sam"@0..3 and an overlapping "am "@1..4

        var result = MentionInsertion.Insert(doc,
            [new MentionSpan(0, "Sam", first, "Sam"), new MentionSpan(1, "am ", second, "x")]);

        Mentions(result).ShouldBe([(first, "Sam")]); // the second overlaps the first → skipped
        ProseMirrorToPlainText.Render(result).ShouldBe("Sam Sam");
    }

    [Theory]
    [InlineData("# Title with Sam\n\nA paragraph naming Sam again", "Sam")]
    [InlineData("- buy milk\n- call Sam back\n- sleep", "Sam")]
    [InlineData("Talked to **Sam** at length", "Sam")]
    public void offsets_from_the_projector_locate_the_name_across_document_shapes(string markdown, string name)
    {
        // The contract that pins this module to ProseMirrorToPlainText: a span built from the projector's
        // own output (the coordinate space the AI reads) lands exactly on the name — across headings, lists,
        // and marks — and wrapping it as a mention leaves the words unchanged.
        var doc = System.Text.Json.Nodes.JsonNode.Parse(
            JournalRecall.Api.Domain.Sessions.Content.MarkdownToProseMirror.ConvertToJson(markdown))!;
        var plaintext = ProseMirrorToPlainText.Render(doc);
        var start = plaintext.IndexOf(name, StringComparison.Ordinal);
        var person = Guid.CreateVersion7();

        var result = MentionInsertion.Insert(doc, [new MentionSpan(start, name, person, name)]);

        Mentions(result).ShouldContain((person, name));
        ProseMirrorToPlainText.Render(result).ShouldBe(plaintext); // words preserved
    }

    [Fact]
    public void is_null_and_empty_safe()
    {
        var person = Guid.CreateVersion7();
        var span = new[] { new MentionSpan(0, "Sam", person, "Sam") };

        MentionInsertion.Insert((JsonNode?)null, span).ShouldBeNull();
        MentionInsertion.Insert(Para(Text("hi")), []).ShouldNotBeNull(); // no spans → returned as-is
        MentionInsertion.Insert("", span).ShouldBe("");
        MentionInsertion.Insert("{ not json", span).ShouldBe("{ not json"); // unparseable → unchanged
    }

    [Fact]
    public void the_string_overload_parses_inserts_and_reserializes()
    {
        var sam = Guid.CreateVersion7();
        var json = Para(Text("Lunch with Sam today")).ToJsonString();

        var result = MentionInsertion.Insert(json, [new MentionSpan(11, "Sam", sam, "Sam")]);

        Mentions(JsonNode.Parse(result)).ShouldBe([(sam, "Sam")]);
    }
}
