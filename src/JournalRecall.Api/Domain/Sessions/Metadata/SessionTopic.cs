namespace JournalRecall.Api.Domain.Sessions.Metadata;

/// <summary>
/// A life-area Topic tag on a Session (CONTEXT.md), free-form and per-user (a Session's tags belong to
/// its owner). Carries provenance so AI Suggestions and user tags coexist without one overwriting the
/// other. Part of the Session aggregate (an owned collection); not an independently-indexed entity.
/// </summary>
public sealed class SessionTopic
{
    public string Name { get; private set; } = string.Empty;
    public MetadataProvenance Provenance { get; private set; }

    private SessionTopic() { } // EF

    internal SessionTopic(string name, MetadataProvenance provenance)
    {
        Name = name;
        Provenance = provenance;
    }
}
