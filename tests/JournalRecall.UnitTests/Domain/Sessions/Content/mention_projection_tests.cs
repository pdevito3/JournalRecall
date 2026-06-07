using System.Text.Json.Nodes;
using JournalRecall.Api.Domain.Sessions.Content;

namespace JournalRecall.UnitTests.Domain.Sessions.Content;

/// <summary>
/// Unit tests for <see cref="MentionProjection"/> (PRD-0006, RICH-006): a Content document projects to the
/// distinct set of directory <c>personId</c>s its <c>mention</c> nodes carry. Mentions are found at any
/// depth, malformed/blank/null input yields an empty set, and mentions without a valid Guid are skipped.
/// No host or DB.
/// </summary>
public class mention_projection_tests
{
    /// <summary>A doc whose paragraph holds the given inline nodes (mentions and/or text).</summary>
    private static JsonNode Doc(params JsonObject[] inline) => new JsonObject
    {
        ["type"] = "doc",
        ["content"] = new JsonArray { new JsonObject { ["type"] = "paragraph", ["content"] = ToArray(inline) } },
    };

    private static JsonObject Mention(string personId, string label = "Sam") => new()
    {
        ["type"] = "mention",
        ["attrs"] = new JsonObject { ["personId"] = personId, ["label"] = label },
    };

    private static JsonObject Text(string text) => new() { ["type"] = "text", ["text"] = text };

    private static JsonArray ToArray(JsonObject[] items)
    {
        var array = new JsonArray();
        foreach (var item in items)
            array.Add(item);
        return array;
    }

    [Fact]
    public void extracts_the_distinct_person_ids_from_mention_nodes()
    {
        var sam = Guid.CreateVersion7();
        var alex = Guid.CreateVersion7();
        var doc = Doc(Mention(sam.ToString(), "Sam"), Text(" and "), Mention(alex.ToString(), "Alex"),
            Mention(sam.ToString(), "Sam")); // repeat collapses

        MentionProjection.ExtractPersonIds(doc).ShouldBe([sam, alex], ignoreOrder: true);
    }

    [Fact]
    public void a_document_with_no_mentions_yields_an_empty_set()
    {
        MentionProjection.ExtractPersonIds(Doc(Text("just words, nobody tagged"))).ShouldBeEmpty();
    }

    [Fact]
    public void mentions_are_found_at_any_depth()
    {
        var sam = Guid.CreateVersion7();
        // A mention nested inside a blockquote → listItem → paragraph is still collected.
        var doc = new JsonObject
        {
            ["type"] = "doc",
            ["content"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "blockquote",
                    ["content"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "paragraph",
                            ["content"] = new JsonArray { Mention(sam.ToString()) },
                        },
                    },
                },
            },
        };

        MentionProjection.ExtractPersonIds(doc).ShouldBe([sam]);
    }

    [Fact]
    public void a_mention_with_a_missing_or_invalid_person_id_is_skipped()
    {
        var valid = Guid.CreateVersion7();
        var noId = new JsonObject { ["type"] = "mention", ["attrs"] = new JsonObject { ["label"] = "Ghost" } };
        var badId = Mention("not-a-guid", "Mangled");

        MentionProjection.ExtractPersonIds(Doc(noId, badId, Mention(valid.ToString()))).ShouldBe([valid]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not json")]
    public void blank_or_malformed_json_yields_an_empty_set(string? json)
    {
        MentionProjection.ExtractPersonIds(json).ShouldBeEmpty();
    }

    [Fact]
    public void the_string_overload_parses_then_projects()
    {
        var sam = Guid.CreateVersion7();
        var json = Doc(Mention(sam.ToString())).ToJsonString();

        MentionProjection.ExtractPersonIds(json).ShouldBe([sam]);
    }
}
