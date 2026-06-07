using System.Text.Json.Nodes;
using JournalRecall.Api.Domain.Sessions.Content;

namespace JournalRecall.SharedTestHelpers.Fakes.Sessions;

/// <summary>
/// Content helpers for tests now that Session Raw/Cleaned content is canonical ProseMirror/tiptap JSON
/// (ADR-0009). <see cref="Doc"/> wraps plain text into a canonical JSON document — exactly what the real
/// editor would serialize and save; <see cref="PlainText"/> projects a stored JSON document back to its
/// derived plain text so a test can assert on the words without pinning the JSON shape.
/// </summary>
public static class ContentDoc
{
    /// <summary>Plain text → a canonical ProseMirror JSON document string (the editor's save payload).</summary>
    public static string Doc(string text) => MarkdownToProseMirror.ConvertToJson(text);

    /// <summary>A stored ProseMirror JSON document string → its derived plain text projection.</summary>
    public static string PlainText(string json) => ProseMirrorToPlainText.Render(json);

    /// <summary>
    /// A canonical document whose single paragraph holds an <c>@</c>-mention node per entry — the payload
    /// the editor saves once a User mentions directory People (PRD-0006). Each mention carries the durable
    /// <c>personId</c> and a display <c>label</c> snapshot.
    /// </summary>
    public static string DocWithMentions(params (Guid PersonId, string Label)[] mentions)
    {
        var content = new JsonArray();
        foreach (var (personId, label) in mentions)
            content.Add(new JsonObject
            {
                ["type"] = "mention",
                ["attrs"] = new JsonObject
                {
                    ["personId"] = personId.ToString(),
                    ["label"] = label,
                },
            });

        return new JsonObject
        {
            ["type"] = "doc",
            ["content"] = new JsonArray
            {
                new JsonObject { ["type"] = "paragraph", ["content"] = content },
            },
        }.ToJsonString();
    }
}
