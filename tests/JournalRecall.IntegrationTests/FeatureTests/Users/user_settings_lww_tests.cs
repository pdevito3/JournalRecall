using JournalRecall.Api.Domain.Users.Features;

namespace JournalRecall.IntegrationTests.FeatureTests.Users;

/// <summary>
/// The skip-if-older rule on the settings full-replace write (ADR-0013, issue 0032): a queued offline
/// settings save older than the last settings save is acknowledged but not applied; newer or
/// timestamp-less writes (the web client) apply exactly as before.
/// </summary>
public class user_settings_lww_tests : TestBase
{
    private static DateTimeOffset At(int hour) => new(2026, 1, 1, hour, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task a_settings_write_saved_before_the_last_settings_save_is_skipped()
    {
        using var scope = new TestingServiceScope();
        await scope.SendAsync(new UpdateUserSettings.Command("America/New_York", false, true, At(12)));

        // Saved offline at 11:00 — before the 12:00 save above → acknowledged, not applied.
        var result = await scope.SendAsync(new UpdateUserSettings.Command("Europe/Paris", true, false, At(11)));

        result.ShouldBe(UpdateUserSettings.Result.Ok);
        var settings = await scope.SendAsync(new GetUserSettings.Query());
        settings.TimeZoneId.ShouldBe("America/New_York");
        settings.LocationCaptureEnabled.ShouldBeFalse();
        settings.RequirePeopleTagApproval.ShouldBeTrue();
    }

    [Fact]
    public async Task a_settings_write_saved_after_the_last_settings_save_is_applied()
    {
        using var scope = new TestingServiceScope();
        await scope.SendAsync(new UpdateUserSettings.Command("America/New_York", false, true, At(12)));

        await scope.SendAsync(new UpdateUserSettings.Command("Europe/Paris", true, false, At(13)));

        var settings = await scope.SendAsync(new GetUserSettings.Query());
        settings.TimeZoneId.ShouldBe("Europe/Paris");
        settings.LocationCaptureEnabled.ShouldBeTrue();
        settings.RequirePeopleTagApproval.ShouldBeFalse();
    }

    [Fact]
    public async Task a_settings_write_without_client_saved_at_applies_and_stamps_the_save_time()
    {
        using var scope = new TestingServiceScope();
        // The web client sends no clientSavedAt — applied as before, stamped at the server clock (12:00).
        await scope.SendAsync(new UpdateUserSettings.Command("America/New_York", true, true));

        // …so a replay the user actually saved earlier still can't clobber it.
        await scope.SendAsync(new UpdateUserSettings.Command("Europe/Paris", false, false, At(11)));

        (await scope.SendAsync(new GetUserSettings.Query())).TimeZoneId.ShouldBe("America/New_York");
    }
}
