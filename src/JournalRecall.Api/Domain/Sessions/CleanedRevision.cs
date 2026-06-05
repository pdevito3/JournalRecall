namespace JournalRecall.Api.Domain.Sessions;

/// <summary>
/// An immutable, appended snapshot of a Session's AI-derived Cleaned copy (ADR-0003). Cleaned has its
/// own Revision stream, separate from Raw: a Cleanup run (or a later hand-edit) appends one here while
/// Raw is left byte-for-byte untouched. A re-run warns-and-overwrites the current Cleaned copy but the
/// prior Revision is retained. Identified within its Session by the 1-based <see cref="RevisionNumber"/>;
/// the EF primary key is a store-generated shadow column. A per-Session drill-down, not part of any
/// list/search index.
/// </summary>
public sealed class CleanedRevision
{
    public int RevisionNumber { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private CleanedRevision() { } // EF

    internal CleanedRevision(int revisionNumber, string content)
    {
        RevisionNumber = revisionNumber;
        Content = content;
    }
}
