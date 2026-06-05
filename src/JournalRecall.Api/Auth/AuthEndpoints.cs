using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using JournalRecall.Api.Domain.Identity;

namespace JournalRecall.Api.Auth;

public static class AuthEndpoints
{
    public sealed record Credentials(string Email, string Password);
    public sealed record UserResponse(Guid Id, string Email, IReadOnlyList<string> Roles);
    /// <summary>Mobile refresh: the refresh token is presented in the body (web uses the HttpOnly cookie).</summary>
    public sealed record RefreshRequest(string? RefreshToken);
    /// <summary>Mobile refresh response: tokens in the body (web gets cookies + an empty body).</summary>
    public sealed record TokenResponse(string AccessToken, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

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

        group.MapPost("/auth/login", async (Credentials body, UserManager<User> users, JwtTokenService tokens,
            RefreshTokenService refreshTokens, HttpRequest request, HttpResponse response) =>
        {
            var user = await users.FindByEmailAsync(body.Email);
            if (user is null || !await users.CheckPasswordAsync(user, body.Password))
                return Results.Unauthorized();

            // A disabled account cannot log in (issue 0016) — checked after the password so it doesn't
            // reveal which accounts exist.
            if (user.IsDisabled)
                return Results.Unauthorized();

            var roles = await users.GetRolesAsync(user);

            // Mint a refresh token for this device first so the access JWT can carry its chain id (ADR-0005).
            var issued = await refreshTokens.IssueAsync(user.Id, DeviceLabel(request));
            var (token, expiresAt) = tokens.Create(user, roles, issued.ChainId);
            AuthCookie.SetAccess(response, token, expiresAt);
            AuthCookie.SetRefresh(response, issued.Token, issued.ExpiresAt);

            return Results.Ok(new UserResponse(user.Id, user.Email!, roles.ToList()));
        });

        // Rotates the refresh token and mints a fresh access JWT. Web reads/writes cookies; mobile passes
        // and receives the tokens in the body (ADR-0005) — the cookie path never returns the refresh
        // token in the body, preserving HttpOnly.
        group.MapPost("/auth/refresh", async (RefreshRequest? body, UserManager<User> users, JwtTokenService tokens,
            RefreshTokenService refreshTokens, HttpRequest request, HttpResponse response) =>
        {
            var fromCookie = request.Cookies[AuthCookie.RefreshName];
            var isCookieFlow = !string.IsNullOrEmpty(fromCookie);
            var presented = fromCookie ?? body?.RefreshToken;
            if (string.IsNullOrEmpty(presented))
                return Results.Unauthorized();

            var rotation = await refreshTokens.RotateAsync(presented, DeviceLabel(request));
            if (!rotation.Succeeded)
            {
                if (isCookieFlow) ClearAuthCookies(response);
                return Results.Unauthorized();
            }

            var user = await users.FindByIdAsync(rotation.UserId.ToString());
            if (user is null || user.IsDisabled)
            {
                // The account vanished or was disabled out from under a live chain — kill it entirely.
                await refreshTokens.RevokeAllAsync(rotation.UserId);
                if (isCookieFlow) ClearAuthCookies(response);
                return Results.Unauthorized();
            }

            var roles = await users.GetRolesAsync(user);
            var (access, accessExpires) = tokens.Create(user, roles, rotation.ChainId);

            if (isCookieFlow)
            {
                AuthCookie.SetAccess(response, access, accessExpires);
                AuthCookie.SetRefresh(response, rotation.Token!, rotation.ExpiresAt!.Value);
                return Results.Ok();
            }

            return Results.Ok(new TokenResponse(access, rotation.Token!, rotation.ExpiresAt!.Value));
        });

        group.MapPost("/auth/logout", async (RefreshTokenService refreshTokens, ClaimsPrincipal principal, HttpResponse response) =>
        {
            // Logout ends this device's session only (ADR-0005). The refresh cookie is path-scoped away
            // from here, so revoke by the access token's chain claim instead of the refresh token value.
            if (Guid.TryParse(principal.FindFirstValue(JwtTokenService.RefreshChainClaim), out var chainId))
                await refreshTokens.RevokeChainAsync(chainId);

            ClearAuthCookies(response);
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

    private static void ClearAuthCookies(HttpResponse response)
    {
        AuthCookie.ClearAccess(response);
        AuthCookie.ClearRefresh(response);
    }

    /// <summary>Best-effort device label from the User-Agent (ADR-0005) — for a future "your sessions"
    /// view; never security-bearing and never IP-bound.</summary>
    private static string? DeviceLabel(HttpRequest request)
    {
        var userAgent = request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(userAgent))
            return null;
        return userAgent.Length > 256 ? userAgent[..256] : userAgent;
    }
}
