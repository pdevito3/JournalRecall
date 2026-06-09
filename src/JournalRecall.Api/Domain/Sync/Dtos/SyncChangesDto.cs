using JournalRecall.Api.Domain.Corrections.Dtos;
using JournalRecall.Api.Domain.People.Dtos;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.Api.Domain.Users.Dtos;

namespace JournalRecall.Api.Domain.Sync.Dtos;

/// <summary>
/// One pull of the delta change feed (issue 0033, ADR-0013): everything of the caller's that changed
/// since the request's cursor — Sessions as their full current state, plus Corrections, People, and the
/// user's Settings (null when unchanged) — and the <see cref="Cursor"/> to replay on the next pull.
/// A bootstrap pull (no cursor) carries the user's full state, Settings included.
/// </summary>
public sealed record SyncChangesDto(
    string Cursor,
    IReadOnlyList<SessionDto> Sessions,
    IReadOnlyList<CorrectionDto> Corrections,
    IReadOnlyList<PersonDto> People,
    UserSettingsDto? Settings);
