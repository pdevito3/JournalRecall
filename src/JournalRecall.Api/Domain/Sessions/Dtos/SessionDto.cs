namespace JournalRecall.Api.Domain.Sessions.Dtos;

public sealed record SessionDto(Guid Id, DateTimeOffset CreatedAt, string RawDraft);
