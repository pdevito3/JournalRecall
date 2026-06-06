using JournalRecall.SharedTestHelpers.Fakes.Ai;

namespace JournalRecall.IntegrationTests;

/// <summary>
/// Base for the serial integration collection. Resets the shared scriptable AI fakes before each test
/// (the collection runs serially, so the mutable switches are safe), and exposes them for scripting.
/// </summary>
[Collection(nameof(TestFixture))]
public abstract class TestBase
{
    protected static ScriptableChatClient CleanupChat => TestFixture.CleanupChat;
    protected static ScriptableSummaryChatClient SummaryChat => TestFixture.SummaryChat;

    /// <summary>The controllable audit clock, reset to a known instant before each test.</summary>
    protected static TestTimeProvider Clock => TestFixture.Clock;

    protected TestBase()
    {
        CleanupChat.Throw = false;
        CleanupChat.CleanedOverride = null;
        CleanupChat.Synopsis = "A short recap of the session.";
        CleanupChat.SuggestTopics = [];
        CleanupChat.SuggestPeople = [];
        CleanupChat.SuggestMood = null;
        SummaryChat.Narrative = "A reflective recap of the period.";
        Clock.Set(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    }
}
