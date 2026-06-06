using QueryKit;
using QueryKit.Configuration;

namespace JournalRecall.Api.Domain.Sessions;

/// <summary>
/// QueryKit configuration for the timeline filter. Exposes friendly query names over the Session's
/// owned metadata collections and mood so a filter like <c>topics == "work"</c> or <c>mood == "Joyful"</c>
/// translates to the right EF predicate (issue 0011). Built-in property names (e.g. <c>CreatedAt</c>)
/// keep working alongside these aliases. There is no <c>people</c> name filter: People are now directory
/// references (no name string on the Session); a PersonId-based filter is a future slice (PRD-0006).
/// </summary>
public static class SessionQueryKitConfig
{
    public static readonly QueryKitConfiguration Instance = new(settings =>
    {
        // Body word-search reads the derived plaintext projection, never the ProseMirror JSON markup,
        // so formatting never hides content from search (ADR-0009).
        settings.Property<Session>(s => s.RawPlainText).HasQueryName("raw");
        settings.Property<Session>(s => s.Topics.Select(t => t.Name)).HasQueryName("topics");
        settings.Property<Session>(s => s.MoodKey!).HasQueryName("mood");
    });
}
