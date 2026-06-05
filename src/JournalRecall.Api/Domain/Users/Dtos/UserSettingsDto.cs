namespace JournalRecall.Api.Domain.Users.Dtos;

/// <summary>
/// Per-user settings. TimeZoneId is null until set (effective zone UTC). LocationCaptureEnabled is the
/// geo opt-in, off by default (CONTEXT.md Location).
/// </summary>
public sealed record UserSettingsDto(string? TimeZoneId, bool LocationCaptureEnabled);
