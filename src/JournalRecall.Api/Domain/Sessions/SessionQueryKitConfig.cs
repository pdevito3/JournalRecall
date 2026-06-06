using QueryKit;
using QueryKit.Configuration;

namespace JournalRecall.Api.Domain.Sessions;

/// <summary>
/// QueryKit configuration for the timeline filter. Exposes friendly query names over the Session's
/// owned metadata so a filter like <c>topics == "work"</c> translates to the right EF predicate
/// (issue 0011). Built-in property names (e.g. <c>CreatedAt</c>) keep working alongside these aliases.
/// There is no <c>people</c> name filter (People are directory references now). Mood is filtered outside
/// QueryKit (see <c>GetSessionList</c>) because it's a JSON primitive collection — QueryKit can't express
/// an any-element match over it, and SQLite needs an EXISTS/contains predicate, not APPLY (PRD-0006).
/// </summary>
public static class SessionQueryKitConfig
{
    public static readonly QueryKitConfiguration Instance = new(settings =>
    {
        // Body word-search reads the derived plaintext projection, never the ProseMirror JSON markup,
        // so formatting never hides content from search (ADR-0009).
        settings.Property<Session>(s => s.RawPlainText).HasQueryName("raw");
        settings.Property<Session>(s => s.Topics.Select(t => t.Name)).HasQueryName("topics");
    });
}
