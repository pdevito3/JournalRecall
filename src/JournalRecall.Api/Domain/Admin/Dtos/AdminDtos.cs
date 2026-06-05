namespace JournalRecall.Api.Domain.Admin.Dtos;

/// <summary>A User as the admin surface sees it — identity + access only, never any journal data (Privacy invariant).</summary>
public sealed record AdminUserDto(Guid Id, string Email, IReadOnlyList<string> Roles, bool IsDisabled);

/// <summary>Create a User from the admin surface (invite/create).</summary>
public sealed record CreateUserRequest(string Email, string Password, string Role);

/// <summary>Assign a User's single role (Admin or Member).</summary>
public sealed record SetRoleRequest(string Role);

/// <summary>
/// The app-wide AI provider config as read by the admin surface. The API key is never returned — only
/// whether one is set — so a stored secret can't leak back out.
/// </summary>
public sealed record AiProviderDto(string Provider, string? Endpoint, string Model, bool HasApiKey);

/// <summary>Set the app-wide AI provider config. A blank ApiKey leaves any existing stored key in place.</summary>
public sealed record AiProviderRequest(string Provider, string? Endpoint, string? ApiKey, string Model);

/// <summary>Whether the instance is open to self-registration (issue 0023). Closed by default.</summary>
public sealed record RegistrationSettingsDto(bool SelfRegistrationEnabled);

/// <summary>Toggle self-registration on or off (Admin-only).</summary>
public sealed record RegistrationSettingsRequest(bool SelfRegistrationEnabled);
