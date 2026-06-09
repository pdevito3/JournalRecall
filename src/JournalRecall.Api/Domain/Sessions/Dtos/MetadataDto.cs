using JournalRecall.Api.Domain.Sessions.Metadata;

namespace JournalRecall.Api.Domain.Sessions.Dtos;

/// <summary>An AI metadata Suggestion awaiting accept/reject (issue 0012). For a Mood, the value is the
/// known mood name or custom text; Topics carry their name.</summary>
public sealed record SuggestionDto(SuggestionKind Kind, string Value);

/// <summary>Accept/reject payload, identifying a Suggestion by its kind + value.</summary>
public sealed record SuggestionRef(SuggestionKind Kind, string Value);

/// <summary>
/// The editable manual metadata for a Session: Topics, Moods (known mood names or custom text), and the
/// single Activity (the canonical name, "None", or custom text). A <b>complete, non-partial</b> payload —
/// the metadata editor holds all fields and always sends the whole object, so the write is a full replace,
/// not a nullable "don't-touch" patch (ADR-0011). People are not here — they project from the prose
/// @-mentions, reconciled on save (PRD-0006, RICH-007). <paramref name="ClientSavedAt"/> is the optional
/// offline-replay save time (ADR-0013, issue 0032): when older than the Session's last write the whole
/// write is skipped; the web client omits it and behaves exactly as before.
/// </summary>
public sealed record MetadataForWrite(
    IReadOnlyList<string> Topics,
    IReadOnlyList<string> Moods,
    string Activity,
    DateTimeOffset? ClientSavedAt = null);
