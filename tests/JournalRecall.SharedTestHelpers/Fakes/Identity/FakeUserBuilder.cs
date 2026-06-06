using Bogus;
using JournalRecall.Api.Domain.Identity;

namespace JournalRecall.SharedTestHelpers.Fakes.Identity;

/// <summary>
/// Seeds a <see cref="User"/> (the tenant boundary) for direct DbContext insertion — bypassing
/// UserManager/Identity, which integration tests never authenticate through (PRD-0003). Sets the
/// normalized lookup fields so Identity's unique indexes are satisfied.
/// </summary>
public class FakeUserBuilder
{
    private static readonly Faker Faker = new();

    private Guid _id = Guid.CreateVersion7();
    private string? _email;
    private string? _timeZoneId;
    private bool _locationCaptureEnabled;
    private bool _isDisabled;
    private bool _mustChangePassword;

    public FakeUserBuilder WithId(Guid id) { _id = id; return this; }

    public FakeUserBuilder WithEmail(string email) { _email = email; return this; }

    public FakeUserBuilder WithTimeZone(string timeZoneId) { _timeZoneId = timeZoneId; return this; }

    public FakeUserBuilder WithLocationCaptureEnabled(bool enabled = true) { _locationCaptureEnabled = enabled; return this; }

    public FakeUserBuilder Disabled() { _isDisabled = true; return this; }

    public FakeUserBuilder MustChangePassword() { _mustChangePassword = true; return this; }

    public User Build()
    {
        var email = _email ?? Faker.Internet.Email();
        return new User
        {
            Id = _id,
            Email = email,
            UserName = email,
            NormalizedEmail = email.ToUpperInvariant(),
            NormalizedUserName = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            TimeZoneId = _timeZoneId,
            LocationCaptureEnabled = _locationCaptureEnabled,
            IsDisabled = _isDisabled,
            MustChangePassword = _mustChangePassword,
        };
    }
}
