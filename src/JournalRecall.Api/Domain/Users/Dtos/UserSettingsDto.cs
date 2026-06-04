namespace JournalRecall.Api.Domain.Users.Dtos;

/// <summary>Per-user settings. TimeZoneId is null until set (effective zone UTC).</summary>
public sealed record UserSettingsDto(string? TimeZoneId);
