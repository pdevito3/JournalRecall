namespace JournalRecall.Api.Domain.Identity;

/// <summary>
/// A server-side, revocable refresh token (ADR-0005). The raw 256-bit value is never persisted — only
/// its SHA-256 hash — and every use rotates it: the current token is revoked, linked to its successor
/// (<see cref="ReplacedByTokenId"/>), and a new token is minted in the same <see cref="ChainId"/>
/// family. Presenting an already-rotated token is reuse-detection (the family is revoked as suspected
/// theft); a brief grace window tolerates a double-fired refresh. Expiry slides on every use with no
/// absolute cap, so an active session is effectively permanent. Not bound to IP (avoids false logouts
/// on network changes); an optional <see cref="DeviceLabel"/> supports a future "your sessions" view.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; private set; }

    /// <summary>The owning User. No tenant query filter applies — refresh runs with no current user.</summary>
    public Guid UserId { get; private set; }

    /// <summary>SHA-256 hash (hex) of the raw token. The raw value is never stored.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    /// <summary>The rotation family: every token minted by rotating a predecessor shares this id, so a
    /// reused token can revoke the entire chain.</summary>
    public Guid ChainId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Sliding expiry: reset to <c>now + InactivityWindow</c> on every issue/rotate.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Set when the token is revoked (rotation, logout, disable, or password change).</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>The successor minted when this token was rotated; null for a token revoked without
    /// rotation (logout/disable/password-change). Distinguishes a benign rotation from a hard revoke.</summary>
    public Guid? ReplacedByTokenId { get; private set; }

    /// <summary>Best-effort device label from the User-Agent (not security-bearing).</summary>
    public string? DeviceLabel { get; private set; }

    private RefreshToken() { } // EF

    public static RefreshToken Issue(
        Guid userId, string tokenHash, Guid chainId,
        DateTimeOffset createdAt, DateTimeOffset expiresAt, string? deviceLabel) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = userId,
        TokenHash = tokenHash,
        ChainId = chainId,
        CreatedAt = createdAt,
        ExpiresAt = expiresAt,
        DeviceLabel = deviceLabel,
    };

    /// <summary>Usable iff it has not been revoked and has not slid past its expiry.</summary>
    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    /// <summary>Whether this token was previously rotated (has a successor) — i.e. a re-presentation is a
    /// rotation double-fire/reuse rather than the use of a hard-revoked token.</summary>
    public bool WasRotated => ReplacedByTokenId is not null;

    /// <summary>Hard-revoke without rotation (logout, Admin-disable, password change). Idempotent.</summary>
    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    /// <summary>Revoke as part of rotation, linking the successor that replaces it.</summary>
    public void MarkReplacedBy(DateTimeOffset now, Guid successorId)
    {
        RevokedAt ??= now;
        ReplacedByTokenId = successorId;
    }
}
