namespace JournalRecall.Api.Domain.Sessions.Dtos;

/// <summary>A Raw Revision in the per-Session history list (no content — keeps the list light).</summary>
public sealed record RawRevisionSummaryDto(int RevisionNumber, DateTimeOffset CreatedAt);

/// <summary>A single Raw Revision with its full snapshot, for rendering a past version.</summary>
public sealed record RawRevisionDto(int RevisionNumber, DateTimeOffset CreatedAt, string Content);
