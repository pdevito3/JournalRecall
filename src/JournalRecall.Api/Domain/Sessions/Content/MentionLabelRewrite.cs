using System.Text.Json;
using System.Text.Json.Nodes;

namespace JournalRecall.Api.Domain.Sessions.Content;

/// <summary>
/// The pure ProseMirror transform that re-labels @-mentions when a directory <see cref="People.Person"/> is
/// renamed (PRD-0006, RICH-005/006): walks a Content document and, on every <c>mention</c> node whose
/// <c>attrs.personId</c> matches the renamed Person, rewrites <c>attrs.label</c> to the new name. The durable
/// <c>personId</c> link is untouched. Pure and deterministic — no I/O, no DB.
/// </summary>
/// <remarks>
/// This keeps the stored JSON canonical so the rename propagates to the prose the editor renders and to the
/// derived plaintext (<see cref="ProseMirrorToPlainText"/>) that feeds search and AI input — rather than
/// only the directory row's label (the badge already resolves live). The walk is shape-agnostic: it recurses
/// into any node's <c>content</c>, so mentions nest anywhere inline text can. Mentions with a missing/invalid
/// <c>personId</c> are left alone, and a null/blank/malformed document is returned unchanged.
/// </remarks>
public static class MentionLabelRewrite
{
    /// <summary>
    /// Rewrites the <c>label</c> of every mention pointing at <paramref name="personId"/> to
    /// <paramref name="newLabel"/>, returning a new document. Null-safe; never throws.
    /// </summary>
    public static JsonNode? Rewrite(JsonNode? doc, Guid personId, string newLabel)
    {
        if (doc is null)
            return doc;

        var copy = doc.DeepClone();
        Apply(copy, personId, newLabel);
        return copy;
    }

    /// <summary>Parses, rewrites, re-serializes. Blank/invalid JSON is returned unchanged.</summary>
    public static string Rewrite(string? json, Guid personId, string newLabel)
    {
        if (string.IsNullOrWhiteSpace(json))
            return json ?? string.Empty;

        JsonNode? doc;
        try { doc = JsonNode.Parse(json); }
        catch (JsonException) { return json; }

        return Rewrite(doc, personId, newLabel)?.ToJsonString() ?? json;
    }

    private static void Apply(JsonNode? node, Guid personId, string newLabel)
    {
        if (node is not JsonObject obj)
            return;

        var type = (obj["type"] as JsonValue)?.GetValue<string>();
        if (type == "mention" &&
            obj["attrs"] is JsonObject attrs &&
            attrs["personId"] is JsonValue personIdValue &&
            personIdValue.TryGetValue<string>(out var raw) &&
            Guid.TryParse(raw, out var id) &&
            id == personId)
            attrs["label"] = newLabel;

        if (obj["content"] is JsonArray content)
            foreach (var child in content)
                Apply(child, personId, newLabel);
    }
}
