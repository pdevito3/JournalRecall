namespace JournalRecall.Api.Domain.Sessions;

/// <summary>
/// An immutable, appended snapshot of a Session's Raw text (ADR-0003). Minted only at save points —
/// never per keystroke — and never altered afterwards. Identified within its Session by the 1-based
/// <see cref="RevisionNumber"/>; the EF primary key is a store-generated shadow column. Part of the
/// Session aggregate; a per-Session drill-down, deliberately not part of any list/search index.
/// </summary>
public sealed class RawRevision
{
    public int RevisionNumber { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private RawRevision() { } // EF

    internal RawRevision(int revisionNumber, string content)
    {
        RevisionNumber = revisionNumber;
        Content = content;
    }
}
