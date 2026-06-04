using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using JournalRecall.Api.Domain.Identity;

namespace JournalRecall.Api.Auth;

public static class AuthEndpoints
{
    public sealed record Credentials(string Email, string Password);
    public sealed record UserResponse(Guid Id, string Email);

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

            return Results.Ok(new UserResponse(user.Id, user.Email!));
        });

        group.MapPost("/auth/login", async (Credentials body, UserManager<User> users, JwtTokenService tokens, HttpResponse response) =>
        {
            var user = await users.FindByEmailAsync(body.Email);
            if (user is null || !await users.CheckPasswordAsync(user, body.Password))
                return Results.Unauthorized();

            var roles = await users.GetRolesAsync(user);
            var (token, expiresAt) = tokens.Create(user, roles);
            AuthCookie.Set(response, token, expiresAt);

            return Results.Ok(new UserResponse(user.Id, user.Email!));
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
            return Results.Ok(new UserResponse(Guid.Parse(id!), email ?? ""));
        }).RequireAuthorization();

        return app;
    }
}
