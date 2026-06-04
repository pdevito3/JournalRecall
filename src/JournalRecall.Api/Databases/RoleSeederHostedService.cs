using Microsoft.AspNetCore.Identity;
using JournalRecall.Api.Domain.Identity;

namespace JournalRecall.Api.Databases;

/// <summary>
/// Ensures the Admin and Member roles exist at startup (after migrations). New registrations are
/// assigned Member; an Admin is granted out-of-band (issue 0016 admin surface, or a seeded first user).
/// </summary>
public sealed class RoleSeederHostedService(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var role in Roles.All)
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
