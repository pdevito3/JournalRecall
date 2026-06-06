using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using JournalRecall.Api.Domain.Identity;

namespace JournalRecall.SharedTestHelpers.Fakes.Identity;

/// <summary>
/// Builds a <see cref="ClaimsPrincipal"/> shaped exactly as the validated production principal
/// (PRD-0003): the <c>sub</c> claim carries the User's id and role claims use <see cref="ClaimTypes.Role"/>,
/// so <c>ICurrentUserService</c> and the HeimGuard policy handler resolve identity and roles just as they
/// do in production. Used to impersonate a User on the mocked <c>IHttpContextAccessor</c>.
/// </summary>
public static class FakeClaimsPrincipal
{
    /// <summary>A principal for the given User id, optional email, and any role claims.</summary>
    public static ClaimsPrincipal ForUser(Guid userId, string? email = null, params string[] roles)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, userId.ToString()) };
        if (email is not null)
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, email));
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "TestAuth"));
    }

    /// <summary>An Admin principal for the given User id (carries the Admin role claim).</summary>
    public static ClaimsPrincipal ForAdmin(Guid userId, string? email = null) =>
        ForUser(userId, email, Roles.Admin);
}
