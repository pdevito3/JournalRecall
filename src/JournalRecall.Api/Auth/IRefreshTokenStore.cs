using JournalRecall.Api.Domain.Identity;

namespace JournalRecall.Api.Auth;

/// <summary>
/// Thin persistence seam for <see cref="RefreshToken"/> so the deep <see cref="RefreshTokenService"/>
/// can be unit-tested against an in-memory fake. There is deliberately no tenant query filter here —
/// refresh runs when the access token has expired, with no current user established.
/// </summary>
public interface IRefreshTokenStore
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RefreshToken>> FindByChainAsync(Guid chainId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RefreshToken>> FindActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
