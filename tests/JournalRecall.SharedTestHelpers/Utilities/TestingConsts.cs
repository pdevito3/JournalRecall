namespace JournalRecall.SharedTestHelpers.Utilities;

/// <summary>
/// Shared constants for the test suite (PRD-0003): stable seed identifiers and the default timezone
/// used when arranging journaling-day-sensitive data. Lives in SharedTestHelpers so every layer reads
/// the same values.
/// </summary>
public static class TestingConsts
{
    /// <summary>A fixed IANA timezone for journaling-day/week/month derivation in tests.</summary>
    public const string DefaultTimeZoneId = "America/New_York";

    /// <summary>A stable User id for tests that want a deterministic tenant rather than a random one.</summary>
    public static readonly Guid DefaultUserId = Guid.Parse("9b1c0f4e-1d2a-4b3c-8e7f-0a1b2c3d4e5f");
}
