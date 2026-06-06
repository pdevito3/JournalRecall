using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using JournalRecall.Api.Domain.Identity;

namespace JournalRecall.Api.Auth;

/// <summary>
/// Mints the single first-party JWT (ADR-0002). The same token is validated whether it arrives as the
/// HttpOnly cookie (web) or an Authorization: Bearer header (mobile).
/// </summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    /// <summary>Identifies the refresh-token chain (device session) this access token belongs to, so a
    /// logout — which receives only the access cookie (Path=/), not the path-scoped refresh cookie — can
    /// revoke exactly the current device's chain (ADR-0005).</summary>
    public const string RefreshChainClaim = "refresh_chain";

    /// <summary>Present (and "true") while the User must replace a temporary password (issue 0024). The
    /// password-change sentinel reads it from the access token; it is re-stamped on refresh from the
    /// current DB state and cleared once the User sets their own password.</summary>
    public const string MustChangePasswordClaim = "must_change_password";

    public (string Token, DateTimeOffset ExpiresAt) Create(
        User user, IEnumerable<string> roles, Guid? refreshChainId = null, bool mustChangePassword = false)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(_options.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.PreferredUsername, user.UserName ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        if (refreshChainId is { } chainId)
            claims.Add(new Claim(RefreshChainClaim, chainId.ToString()));
        if (mustChangePassword)
            claims.Add(new Claim(MustChangePasswordClaim, "true"));
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }
}
