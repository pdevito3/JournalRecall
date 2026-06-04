using System.Security.Claims;
using HeimGuard;
using JournalRecall.Api.Domain.Identity;

namespace JournalRecall.Api.Auth;

/// <summary>
/// Resolves the current user's permissions from their role claims (ADR-0002). The mapping is
/// deliberately code-local and tiny for now: Admin ⇒ admin-surface access. HeimGuard turns each
/// permission into an authorization policy via MapAuthorizationPolicies().
/// </summary>
internal sealed class UserPolicyHandler(IHttpContextAccessor httpContextAccessor) : IUserPolicyHandler
{
    public Task<IEnumerable<string>> GetUserPermissions()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        var roles = principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet() ?? [];

        var permissions = new HashSet<string>();
        if (roles.Contains(Roles.Admin))
            permissions.Add(Permissions.CanAccessAdmin);

        return Task.FromResult<IEnumerable<string>>(permissions);
    }
}
