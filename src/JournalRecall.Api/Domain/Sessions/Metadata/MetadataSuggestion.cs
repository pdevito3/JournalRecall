using System.Text.Json.Serialization;

namespace JournalRecall.Api.Domain.Sessions.Metadata;

/// <summary>What a <see cref="MetadataSuggestion"/> proposes.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SuggestionKind>))]
public enum SuggestionKind
{
    Topic,
    Person,
    Mood,
}

/// <summary>
/// An AI-proposed piece of metadata awaiting the user's accept/reject (CONTEXT.md). Not yet
/// authoritative metadata — it lives in its own pending list on the Session until accepted (promoted to
/// metadata with provenance <see cref="MetadataProvenance.AiSuggested"/>) or rejected (discarded). Part
/// of the Session aggregate (an owned collection).
/// </summary>
public sealed class MetadataSuggestion
{
    public SuggestionKind Kind { get; private set; }

    /// <summary>The proposed value: a Topic name, or a Mood (a known mood name or custom text).</summary>
    public string Value { get; private set; } = string.Empty;

    private MetadataSuggestion() { } // EF

    internal MetadataSuggestion(SuggestionKind kind, string value)
    {
        Kind = kind;
        Value = value;
    }

    internal bool Matches(SuggestionKind kind, string value) =>
        Kind == kind && Value.Equals(value, StringComparison.OrdinalIgnoreCase);
}
