using JournalRecall.Api.Domain.Sessions.Metadata;

namespace JournalRecall.Api.Domain.Sessions.Dtos;

/// <summary>An AI metadata Suggestion awaiting accept/reject (issue 0012). For a Mood, the value is the
/// known mood name or custom text; Topics carry their name.</summary>
public sealed record SuggestionDto(SuggestionKind Kind, string Value);

/// <summary>Accept/reject payload, identifying a Suggestion by its kind + value.</summary>
public sealed record SuggestionRef(SuggestionKind Kind, string Value);

/// <summary>
/// The editable manual metadata for a Session. Moods are plain strings (known mood names or custom text);
/// People are labels the server resolves to directory references.
/// </summary>
public sealed record MetadataForWrite(
    IReadOnlyList<string>? Topics,
    IReadOnlyList<string>? People,
    IReadOnlyList<string>? Moods);
