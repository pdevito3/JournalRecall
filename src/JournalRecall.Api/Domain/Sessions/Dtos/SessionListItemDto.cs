namespace JournalRecall.Api.Domain.Sessions.Dtos;

/// <summary>
/// A Session row in the reverse-chronological timeline. Carries the derived <see cref="JournalingDay"/>
/// (in the user's timezone) so the UI can group by day, plus a short Raw preview. Reflects current
/// state only — historical Revisions never appear as separate rows.
/// </summary>
public sealed record SessionListItemDto(Guid Id, DateTimeOffset CreatedAt, DateOnly JournalingDay, string Preview);
