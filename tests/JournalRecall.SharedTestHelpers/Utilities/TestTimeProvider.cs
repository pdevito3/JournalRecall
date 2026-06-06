namespace JournalRecall.SharedTestHelpers.Utilities;

/// <summary>
/// A controllable <see cref="TimeProvider"/> so the audit clock (and anything else time-dependent) is
/// deterministic and advanceable in tests (PRD-0003). The Api's <c>TimeProvider.System</c> registration
/// is swapped for this in the test host.
/// </summary>
public sealed class TestTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>Advances the clock by the given span.</summary>
    public void Advance(TimeSpan by) => _now += by;

    /// <summary>Resets the clock to a fixed instant (used to make each test start from a known time).</summary>
    public void Set(DateTimeOffset now) => _now = now;
}
