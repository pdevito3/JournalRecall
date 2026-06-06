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
}
