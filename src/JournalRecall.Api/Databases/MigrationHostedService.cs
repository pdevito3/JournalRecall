using Microsoft.EntityFrameworkCore;

namespace JournalRecall.Api.Databases;

/// <summary>
/// Applies EF Core migrations for <typeparamref name="TDbContext"/> at startup so the SQLite file and
/// schema exist on first run. Single-process, file-based SQLite needs no cross-instance locking
/// (ADR-0001). No-ops when no connection string is configured.
/// </summary>
public sealed class MigrationHostedService<TDbContext>(
    IServiceScopeFactory scopeFactory,
    ILogger<MigrationHostedService<TDbContext>> logger) : IHostedService
    where TDbContext : DbContext
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        var connectionString = context.Database.GetConnectionString();
        if (string.IsNullOrEmpty(connectionString))
        {
            logger.LogWarning("No connection string for {DbContext}; skipping migrations.", typeof(TDbContext).Name);
            return;
        }

        logger.LogInformation("Applying migrations for {DbContext}", typeof(TDbContext).Name);
        await context.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Migrations complete for {DbContext}", typeof(TDbContext).Name);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
