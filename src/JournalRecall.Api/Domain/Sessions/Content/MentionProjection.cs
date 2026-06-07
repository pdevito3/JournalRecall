using System.Text.Json;
using System.Text.Json.Nodes;

namespace JournalRecall.Api.Domain.Sessions.Content;

/// <summary>
/// The pure seam from canonical <b>Content</b> (ProseMirror/tiptap JSON) to the directory
/// <see cref="People.Person"/>s it references: walks a document and returns the set of <c>personId</c>s
/// carried by its <c>mention</c> nodes (PRD-0006, RICH-006). Pure and deterministic — no I/O, no DB.
/// This is how the People <b>Metadata</b> stays a projection of the prose rather than an editable field.
/// </summary>
/// <remarks>
/// A <c>mention</c> node carries <c>attrs.personId</c> (the durable link) and <c>attrs.label</c> (a
/// display snapshot — ignored here). Mentions whose <c>personId</c> is missing or not a Guid are skipped
/// rather than throwing, so a partially-formed or hand-mangled document never breaks a save. The walk is
/// shape-agnostic: it recurses into any node's <c>content</c>, so mentions nest anywhere inline text can.
/// </remarks>
public static class MentionProjection
{
    /// <summary>Extracts the distinct <c>personId</c> set from a Content document. Null-safe; never throws.</summary>
    public static IReadOnlySet<Guid> ExtractPersonIds(JsonNode? doc)
    {
        var ids = new HashSet<Guid>();
        Collect(doc, ids);
        return ids;
    }

    /// <summary>Parses Content JSON, then extracts. Null/blank/invalid JSON yields an empty set.</summary>
    public static IReadOnlySet<Guid> ExtractPersonIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new HashSet<Guid>();

        JsonNode? doc;
        try
        {
            doc = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return new HashSet<Guid>();
        }

        return ExtractPersonIds(doc);
    }

    /// <summary>
    /// Recursively walks the node tree: a <c>mention</c> contributes its <c>attrs.personId</c> (when a
    /// valid Guid); every node recurses into its <c>content</c> so mentions are found at any depth.
    /// </summary>
    private static void Collect(JsonNode? node, HashSet<Guid> ids)
    {
        if (node is not JsonObject obj)
            return;

        var type = (obj["type"] as JsonValue)?.GetValue<string>();
        if (type == "mention" &&
            obj["attrs"] is JsonObject attrs &&
            attrs["personId"] is JsonValue personIdValue &&
            personIdValue.TryGetValue<string>(out var raw) &&
            Guid.TryParse(raw, out var personId))
            ids.Add(personId);

        if (obj["content"] is JsonArray content)
            foreach (var child in content)
                Collect(child, ids);
    }
}
