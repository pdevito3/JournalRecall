namespace JournalRecall.Api.Domain.Admin;

/// <summary>
/// App-wide authentication settings (issue 0023, PRD-0001): a single row per installation, set only by
/// an Admin, mirroring <see cref="AiProviderSettings"/>. Holds whether the instance is open to
/// self-registration — <strong>closed by default</strong>, so a fresh private journal admits only Users
/// an Admin adds, until the operator deliberately opens it. Lazy-created on first write.
/// </summary>
public sealed class AuthSettings : BaseEntity
{
    public bool SelfRegistrationEnabled { get; private set; }

    private AuthSettings() { } // EF

    public static AuthSettings Create() => new();

    public void SetSelfRegistration(bool enabled) => SelfRegistrationEnabled = enabled;
}
