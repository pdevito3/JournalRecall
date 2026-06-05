namespace JournalRecall.Api.Domain.Corrections.Dtos;

/// <summary>A Correction as the client sees it.</summary>
public sealed record CorrectionDto(
    Guid Id,
    string CanonicalTerm,
    IReadOnlyList<string> Mishearings,
    bool HardReplace,
    DateTimeOffset CreatedAt);

/// <summary>Create/update payload (no id — the route or a fresh entity supplies it).</summary>
public sealed record CorrectionForWrite(string CanonicalTerm, IReadOnlyList<string>? Mishearings, bool HardReplace);
