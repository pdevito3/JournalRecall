using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Identity;

namespace JournalRecall.Api.Auth;

/// <summary>EF-backed <see cref="IRefreshTokenStore"/> over the single app DbContext.</summary>
internal sealed class EfRefreshTokenStore(JournalRecallDbContext db) : IRefreshTokenStore
{
    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default) =>
        await db.RefreshTokens.AddAsync(token, cancellationToken);

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> FindByChainAsync(Guid chainId, CancellationToken cancellationToken = default) =>
        await db.RefreshTokens.Where(t => t.ChainId == chainId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> FindActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
