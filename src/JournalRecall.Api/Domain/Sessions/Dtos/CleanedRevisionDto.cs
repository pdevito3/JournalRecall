namespace JournalRecall.Api.Domain.Sessions.Dtos;

/// <summary>A Cleaned Revision in the per-Session history list (no content — keeps the list light).</summary>
public sealed record CleanedRevisionSummaryDto(int RevisionNumber, DateTimeOffset CreatedAt);

/// <summary>A single Cleaned Revision with its full snapshot, for rendering a past version.</summary>
public sealed record CleanedRevisionDto(int RevisionNumber, DateTimeOffset CreatedAt, string Content);
