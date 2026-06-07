namespace JournalRecall.Api.Domain.Users.Dtos;

/// <summary>
/// Per-user settings. TimeZoneId is null until set (effective zone UTC). LocationCaptureEnabled is the
/// geo opt-in, off by default (CONTEXT.md Location). RequirePeopleTagApproval gates AI People-tagging,
/// on by default (PRD-0006, RICH-009).
/// </summary>
public sealed record UserSettingsDto(string? TimeZoneId, bool LocationCaptureEnabled, bool RequirePeopleTagApproval);
