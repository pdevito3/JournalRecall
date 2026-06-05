namespace JournalRecall.Api.Domain.Sessions.Metadata;

/// <summary>
/// A Person referenced in a Session (CONTEXT.md): a free-form name, per-user, with provenance so AI
/// Suggestions and user tags coexist. Part of the Session aggregate (an owned collection).
/// </summary>
public sealed class SessionPerson
{
    public string Name { get; private set; } = string.Empty;
    public MetadataProvenance Provenance { get; private set; }

    private SessionPerson() { } // EF

    internal SessionPerson(string name, MetadataProvenance provenance)
    {
        Name = name;
        Provenance = provenance;
    }
}
