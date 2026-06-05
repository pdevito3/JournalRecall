using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Admin;

namespace JournalRecall.Api.Auth;

/// <summary>
/// Reader/writer for the app-wide <see cref="AuthSettings"/> singleton (issue 0023), mirroring how the
/// AI provider config is accessed. The row is lazy-created on first write; an unset instance reads as the
/// secure default (self-registration <em>off</em>).
/// </summary>
public sealed class AuthSettingsService(JournalRecallDbContext db)
{
    public async Task<bool> SelfRegistrationEnabledAsync(CancellationToken cancellationToken = default)
    {
        var settings = await db.AuthSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return settings?.SelfRegistrationEnabled ?? false; // default closed
    }

    public async Task SetSelfRegistrationAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        var settings = await db.AuthSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = AuthSettings.Create();
            db.AuthSettings.Add(settings);
        }

        settings.SetSelfRegistration(enabled);
        await db.SaveChangesAsync(cancellationToken);
    }
}
