namespace JournalRecall.FunctionalTests.GlobalState;

/// <summary>
/// A functional host for <b>app-global</b> tests (PRD-0003, ADR-0006): first-run setup (zero Users),
/// the access gate, and the registration policy — state that isn't User-scoped and can't share the
/// pooled functional host. Each such test boots its own instance (<c>using var</c>) for a clean
/// database and the real <b>closed</b> registration default (it does not seed self-registration).
/// </summary>
public sealed class GlobalStateWebApplicationFactory : FunctionalWebApplicationFactory
{
    protected override bool SeedSelfRegistration => false;
}
