using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Domain.Identity;

namespace JournalRecall.Api.Auth;

/// <summary>
/// First-run onboarding (issue 0021, PRD-0001): a brand-new instance with zero Users lets the operator
/// create the first account, which is automatically the root <strong>Admin</strong>. The endpoint is
/// anonymous but refuses (409) once any User exists, so it cannot be hijacked later. It bypasses
/// self-registration entirely — bootstrapping an instance is not registration — and the operator types
/// their own password (no temporary-password flag).
/// </summary>
public static class SetupEndpoints
{
    /// <summary>
    /// Single-process, file-based SQLite (ADR-0001) needs no cross-instance locking: a process-level gate
    /// makes the zero-Users check-then-create atomic, so concurrent first-run attempts resolve to exactly
    /// one root Admin (the losers see the User now exists and get 409).
    /// </summary>
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static IEndpointRouteBuilder MapSetup(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/setup", async (AuthEndpoints.Credentials body, UserManager<User> users) =>
        {
            await Gate.WaitAsync();
            try
            {
                if (await users.Users.AnyAsync())
                    return Results.Conflict(); // already set up — refuse forever

                // Username.Create validates format/length (throws → 422); User.Create is the sole path.
                var user = User.Create(Username.Create(body.Username));
                var result = await users.CreateAsync(user, body.Password);
                if (!result.Succeeded)
                    return Results.ValidationProblem(result.Errors.GroupBy(e => e.Code)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));

                await users.AddToRoleAsync(user, Roles.Admin); // the root Admin
                return Results.Ok(new AuthEndpoints.UserResponse(user.Id, user.UserName!, [Roles.Admin]));
            }
            finally
            {
                Gate.Release();
            }
        });

        return app;
    }
}
