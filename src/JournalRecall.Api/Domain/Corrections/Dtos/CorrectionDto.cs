namespace JournalRecall.Api.Domain.Corrections.Dtos;

/// <summary>A Correction as the client sees it.</summary>
public sealed record CorrectionDto(
    Guid Id,
    string CanonicalTerm,
    IReadOnlyList<string> Mishearings,
    bool HardReplace,
    DateTimeOffset CreatedAt);

/// <summary>Create/update payload (no id — the route or a fresh entity supplies it). ClientSavedAt is
/// the optional offline-replay save time (ADR-0013, issue 0032): an update older than the Correction's
/// last write is skipped; the web client omits it and behaves exactly as before. Ignored on create.</summary>
public sealed record CorrectionForWrite(
    string CanonicalTerm,
    IReadOnlyList<string>? Mishearings,
    bool HardReplace,
    DateTimeOffset? ClientSavedAt = null);
