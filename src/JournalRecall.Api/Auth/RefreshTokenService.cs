using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using JournalRecall.Api.Domain.Identity;

namespace JournalRecall.Api.Auth;

/// <summary>
/// The encapsulated refresh-token lifecycle (ADR-0005): issue, rotate-on-use, and revoke. The raw token
/// is a 256-bit random value returned to the caller exactly once and stored only as a SHA-256 hash.
/// Rotation revokes the presented token and mints a successor in the same chain; presenting an
/// already-rotated token revokes the whole chain as suspected theft, except within a short grace window
/// where it is treated as a benign double-fire. Expiry slides on every use with no absolute cap.
/// </summary>
public sealed class RefreshTokenService(
    IRefreshTokenStore store, IOptions<RefreshTokenOptions> options, TimeProvider timeProvider)
{
    private readonly RefreshTokenOptions _options = options.Value;

    /// <summary>A freshly minted raw token plus its sliding expiry and chain. The raw value is never persisted.</summary>
    public sealed record Issued(string Token, DateTimeOffset ExpiresAt, Guid ChainId);

    /// <summary>The outcome of a rotation: on success a new raw token + expiry + chain for the same User
    /// (plus the chain's bound key thumbprint when it is a DPoP-bound chain, so the caller mints a bound
    /// access token — ADR-0014); on failure (unknown/expired/reused/wrong-key token) no token, and the
    /// caller must reject the refresh.</summary>
    public sealed record Rotation(
        bool Succeeded, Guid UserId, string? Token, DateTimeOffset? ExpiresAt, Guid ChainId, string? BoundKeyThumbprint = null)
    {
        public static Rotation Fail() => new(false, Guid.Empty, null, null, Guid.Empty);
        public static Rotation Ok(Guid userId, Issued issued, string? boundKeyThumbprint) =>
            new(true, userId, issued.Token, issued.ExpiresAt, issued.ChainId, boundKeyThumbprint);
    }

    /// <summary>Issues the first token of a brand-new chain (called on login). A non-null
    /// <paramref name="boundKeyThumbprint"/> binds the chain to that DPoP device key (ADR-0014).</summary>
    public async Task<Issued> IssueAsync(Guid userId, string? deviceLabel, string? boundKeyThumbprint = null,
        CancellationToken cancellationToken = default)
    {
        var (issued, _) = await MintAsync(userId, Guid.CreateVersion7(), deviceLabel, boundKeyThumbprint, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return issued;
    }

    /// <summary>Side-effect-free existence probe for a presented raw token (issue 0042). The refresh
    /// endpoint calls this before validating a DPoP proof so an anonymous caller with a garbage token and
    /// a self-signed proof can't burn an ES256 verify or seed the replay cache against a token that was
    /// never issued. Because it neither rotates nor revokes, it preserves "an invalid proof never burns
    /// the rotation".</summary>
    public async Task<bool> ExistsAsync(string rawToken, CancellationToken cancellationToken = default) =>
        await store.FindByHashAsync(Hash(rawToken), cancellationToken) is not null;

    /// <summary>Rotates a presented token: validate → revoke current + mint successor in the same chain.
    /// Reuse outside the grace window revokes the whole chain. For a DPoP-bound chain the caller passes
    /// the presented proof's key thumbprint; a mismatch (or missing proof) is suspected theft and revokes
    /// the chain. Unbound chains ignore <paramref name="presentedKeyThumbprint"/> and rotate as today.</summary>
    public async Task<Rotation> RotateAsync(string rawToken, string? deviceLabel, string? presentedKeyThumbprint = null,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var current = await store.FindByHashAsync(Hash(rawToken), cancellationToken);
        if (current is null)
            return Rotation.Fail();

        // A bound chain rotates only with proof-of-possession of its key (ADR-0014). Checked before the
        // grace window: a stolen refresh token without the device key never gets a token, even inside
        // the window — and the whole chain dies, consistent with reuse-detection.
        if (current.BoundKeyThumbprint is { } bound && bound != presentedKeyThumbprint)
        {
            await RevokeChainNoSaveAsync(current.ChainId, now, cancellationToken);
            await store.SaveChangesAsync(cancellationToken);
            return Rotation.Fail();
        }

        if (current.IsActive(now))
        {
            var (issued, successorId) = await MintAsync(
                current.UserId, current.ChainId, deviceLabel, current.BoundKeyThumbprint, cancellationToken);
            current.MarkReplacedBy(now, successorId);
            await store.SaveChangesAsync(cancellationToken);
            return Rotation.Ok(current.UserId, issued, current.BoundKeyThumbprint);
        }

        // Revoked-and-rotated token re-presented: a benign double-fire inside the grace window gets a
        // fresh token; outside it, this is reuse → revoke the entire chain as suspected theft.
        if (current.WasRotated && current.RevokedAt is { } revokedAt)
        {
            if (now - revokedAt <= _options.GraceWindow)
            {
                var (issued, _) = await MintAsync(
                    current.UserId, current.ChainId, deviceLabel, current.BoundKeyThumbprint, cancellationToken);
                await store.SaveChangesAsync(cancellationToken);
                return Rotation.Ok(current.UserId, issued, current.BoundKeyThumbprint);
            }

            await RevokeChainNoSaveAsync(current.ChainId, now, cancellationToken);
            await store.SaveChangesAsync(cancellationToken);
            return Rotation.Fail();
        }

        // Expired (never rotated) or hard-revoked (logout/disable/password-change): simply reject.
        return Rotation.Fail();
    }

    /// <summary>Revokes the single token for the current device (logout via the refresh token value). No-op
    /// if unknown/already revoked.</summary>
    public async Task RevokeCurrentAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        var token = await store.FindByHashAsync(Hash(rawToken), cancellationToken);
        if (token is null)
            return;
        token.Revoke(timeProvider.GetUtcNow());
        await store.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Revokes the whole chain for the current device (logout via the access token's chain claim,
    /// since the refresh cookie is path-scoped away from the logout endpoint).</summary>
    public async Task RevokeChainAsync(Guid chainId, CancellationToken cancellationToken = default)
    {
        await RevokeChainNoSaveAsync(chainId, timeProvider.GetUtcNow(), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Revokes every active token for a User (Admin-disable, password change).</summary>
    public async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var token in await store.FindActiveByUserAsync(userId, cancellationToken))
            token.Revoke(now);
        await store.SaveChangesAsync(cancellationToken);
    }

    private async Task<(Issued Issued, Guid Id)> MintAsync(
        Guid userId, Guid chainId, string? deviceLabel, string? boundKeyThumbprint, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var raw = Base64Url(RandomNumberGenerator.GetBytes(32)); // 256 bits of entropy
        var expiresAt = now + _options.InactivityWindow;
        var token = RefreshToken.Issue(userId, Hash(raw), chainId, now, expiresAt, deviceLabel, boundKeyThumbprint);
        await store.AddAsync(token, cancellationToken);
        return (new Issued(raw, expiresAt, chainId), token.Id);
    }

    private async Task RevokeChainNoSaveAsync(Guid chainId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var token in await store.FindByChainAsync(chainId, cancellationToken))
            token.Revoke(now);
    }

    private static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
