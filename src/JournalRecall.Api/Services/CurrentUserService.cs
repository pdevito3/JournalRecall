using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using JournalRecall.Api.Domain.Identity;

namespace JournalRecall.Api.Services;

/// <summary>
/// The current user derived from the validated principal — the single place handlers read identity
/// and role from, so tenancy and authorization behave identically across endpoints (ADR-0002).
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? UserName { get; }
    IReadOnlySet<string> Roles { get; }
    bool IsAdmin { get; }
}

internal sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    public string? UserName => Principal?.FindFirstValue(JwtRegisteredClaimNames.PreferredUsername);

    public IReadOnlySet<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet() ?? new HashSet<string>();

    public bool IsAdmin => Roles.Contains(Domain.Identity.Roles.Admin);
}
