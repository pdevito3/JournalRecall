namespace JournalRecall.Api.Domain.Sessions.Dtos;

/// <summary>A mood on the wire: a known key or "Custom", with the free text for Custom.</summary>
public sealed record MoodDto(string Key, string? CustomValue);

/// <summary>The editable manual metadata for a Session (all provenance UserSet).</summary>
public sealed record MetadataForWrite(
    IReadOnlyList<string>? Topics,
    IReadOnlyList<string>? People,
    MoodDto? Mood);
