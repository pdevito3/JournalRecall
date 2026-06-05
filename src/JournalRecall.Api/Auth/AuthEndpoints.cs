using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using JournalRecall.Api.Domain.Identity;
using JournalRecall.Api.Services;

namespace JournalRecall.Api.Auth;

public static class AuthEndpoints
{
    public sealed record Credentials(string Email, string Password);
    public sealed record UserResponse(Guid Id, string Email, IReadOnlyList<string> Roles);

    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api");

        group.MapPost("/auth/register", async (Credentials body, UserManager<User> users) =>
        {
            var user = new User { UserName = body.Email, Email = body.Email };
            var result = await users.CreateAsync(user, body.Password);
            if (!result.Succeeded)
                return Results.ValidationProblem(result.Errors.GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));

            await users.AddToRoleAsync(user, Roles.Member); // Member is the default role
            return Results.Ok(new UserResponse(user.Id, user.Email!, [Roles.Member]));
        });

        group.MapPost("/auth/login", async (Credentials body, UserManager<User> users, JwtTokenService tokens, HttpResponse response) =>
        {
            var user = await users.FindByEmailAsync(body.Email);
            if (user is null || !await users.CheckPasswordAsync(user, body.Password))
                return Results.Unauthorized();

            // A disabled account cannot log in (issue 0016) — checked after the password so it doesn't
            // reveal which accounts exist.
            if (user.IsDisabled)
                return Results.Unauthorized();

            var roles = await users.GetRolesAsync(user);
            var (token, expiresAt) = tokens.Create(user, roles);
            AuthCookie.Set(response, token, expiresAt);

            return Results.Ok(new UserResponse(user.Id, user.Email!, roles.ToList()));
        });

        group.MapPost("/auth/logout", (HttpResponse response) =>
        {
            AuthCookie.Clear(response);
            return Results.NoContent();
        });

        group.MapGet("/me", (ClaimsPrincipal principal) =>
        {
            var id = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var email = principal.FindFirstValue(JwtRegisteredClaimNames.Email);
            var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            return Results.Ok(new UserResponse(Guid.Parse(id!), email ?? "", roles));
        }).RequireAuthorization();

        return app;
    }
}
