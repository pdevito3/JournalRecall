using JournalRecall.Api.Domain.Sessions.Metadata;

namespace JournalRecall.Api.Domain.Sessions.Dtos;

/// <summary>A mood on the wire: a known key or "Custom", with the free text for Custom.</summary>
public sealed record MoodDto(string Key, string? CustomValue);

/// <summary>An AI metadata Suggestion awaiting accept/reject (issue 0012).</summary>
public sealed record SuggestionDto(SuggestionKind Kind, string Value, string? MoodCustomValue);

/// <summary>Accept/reject payload, identifying a Suggestion by its kind + value.</summary>
public sealed record SuggestionRef(SuggestionKind Kind, string Value);

/// <summary>The editable manual metadata for a Session (all provenance UserSet).</summary>
public sealed record MetadataForWrite(
    IReadOnlyList<string>? Topics,
    IReadOnlyList<string>? People,
    MoodDto? Mood);
