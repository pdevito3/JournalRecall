using JournalRecall.Api.Domain.Identity;

namespace JournalRecall.SharedTestHelpers.Fakes.Identity;

/// <summary>
/// Seeds a <see cref="User"/> (the tenant boundary) for direct DbContext insertion — bypassing
/// UserManager/Identity, which integration tests never authenticate through (PRD-0003). Sets the
/// normalized username so Identity's unique index is satisfied. Username is the identity now (issue
/// 0027); the inherited Email columns are left null/unused.
/// </summary>
public class FakeUserBuilder
{
    private Guid _id = Guid.CreateVersion7();
    private string? _userName;
    private string? _timeZoneId;
    private bool _locationCaptureEnabled;
    private bool _isDisabled;
    private bool _mustChangePassword;

    public FakeUserBuilder WithId(Guid id) { _id = id; return this; }

    public FakeUserBuilder WithUserName(string userName) { _userName = userName; return this; }

    public FakeUserBuilder WithTimeZone(string timeZoneId) { _timeZoneId = timeZoneId; return this; }

    public FakeUserBuilder WithLocationCaptureEnabled(bool enabled = true) { _locationCaptureEnabled = enabled; return this; }

    public FakeUserBuilder Disabled() { _isDisabled = true; return this; }

    public FakeUserBuilder MustChangePassword() { _mustChangePassword = true; return this; }

    public User Build()
    {
        var userName = _userName ?? $"user_{Guid.NewGuid():N}"[..18];
        return new User
        {
            Id = _id,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            TimeZoneId = _timeZoneId,
            LocationCaptureEnabled = _locationCaptureEnabled,
            IsDisabled = _isDisabled,
            MustChangePassword = _mustChangePassword,
        };
    }
}
