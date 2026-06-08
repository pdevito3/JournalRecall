using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Identity;

namespace JournalRecall.Api.Auth;

public static class AuthEndpoints
{
    public sealed record Credentials(string Username, string Password);
    public sealed record UserResponse(Guid Id, string Username, IReadOnlyList<string> Roles, bool MustChangePassword = false);
    /// <summary>Change-own-password (issue 0024): clears the forced-change flag and revokes other sessions.</summary>
    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
    /// <summary>Public config that drives all anonymous routing (server gate + client guard, issue 0022).</summary>
    /// <param name="AiConfigured">An Admin has set an AI provider — the cue the client uses to enable AI cleanup.</param>
    public sealed record AuthConfigResponse(bool NeedsSetup, bool SelfRegistrationEnabled, bool AiConfigured);
    /// <summary>Mobile refresh: the refresh token is presented in the body (web uses the HttpOnly cookie).</summary>
    public sealed record RefreshRequest(string? RefreshToken);
    /// <summary>Mobile refresh response: tokens in the body (web gets cookies + an empty body).</summary>
    public sealed record TokenResponse(string AccessToken, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api");

        // Anonymous: drives the access gate and onboarding routing. needsSetup = zero Users exist;
        // selfRegistrationEnabled is the operator-controlled toggle (issue 0023).
        group.MapGet("/auth/config", async (UserManager<User> users, AuthSettingsService authSettings, JournalRecallDbContext db) =>
        {
            var needsSetup = !await users.Users.AnyAsync();
            // aiConfigured = an Admin has set the app-wide provider (non-empty model). Surfaced here, on the
            // always-reachable config, because the AI-provider endpoint itself is Admin-only — a Member needs
            // this to know whether the Cleanup button can do anything.
            var aiSettings = await db.AiProviderSettings.AsNoTracking().FirstOrDefaultAsync();
            return Results.Ok(new AuthConfigResponse(needsSetup, await authSettings.SelfRegistrationEnabledAsync(), aiSettings?.IsConfigured ?? false));
        });

        group.MapPost("/auth/register", async (Credentials body, UserManager<User> users, AuthSettingsService authSettings) =>
        {
            // Operator-controlled: a closed instance rejects self-registration server-side (issue 0023).
            if (!await authSettings.SelfRegistrationEnabledAsync())
                return Results.Problem("Self-registration is disabled.", statusCode: StatusCodes.Status403Forbidden);

            // Username.Create validates format/length (throws → 422); User.Create is the sole path.
            var user = User.Create(Username.Create(body.Username));
            var result = await users.CreateAsync(user, body.Password);
            if (!result.Succeeded)
                return Results.ValidationProblem(result.Errors.GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));

            await users.AddToRoleAsync(user, Roles.Member); // Member is the default role
            return Results.Ok(new UserResponse(user.Id, user.UserName!, [Roles.Member]));
        });

        group.MapPost("/auth/login", async (Credentials body, UserManager<User> users, JwtTokenService tokens,
            RefreshTokenService refreshTokens, IOptions<RefreshTokenOptions> refreshOptions, HttpRequest request, HttpResponse response) =>
        {
            var user = await users.FindByNameAsync(body.Username);
            if (user is null || !await users.CheckPasswordAsync(user, body.Password))
                return Results.Unauthorized();

            // A disabled account cannot log in (issue 0016) — checked after the password so it doesn't
            // reveal which accounts exist.
            if (user.IsDisabled)
                return Results.Unauthorized();

            var roles = await users.GetRolesAsync(user);

            // Mint a refresh token for this device first so the access JWT can carry its chain id (ADR-0005).
            var issued = await refreshTokens.IssueAsync(user.Id, DeviceLabel(request));
            var (token, _) = tokens.Create(user, roles, issued.ChainId, user.MustChangePassword);
            // Cookie lifetime is pure transport on the real wall clock: long enough that a returning User
            // isn't bounced to /login by the server access-gate on a cold load. The JWT inside still
            // expires in ~15 min and is silently rotated (issue 0022 / ADR-0005); the token's *domain*
            // expiry uses the injectable clock, the cookie does not.
            var cookieExpiry = CookieExpiry(refreshOptions);
            AuthCookie.SetAccess(response, token, cookieExpiry);
            AuthCookie.SetRefresh(response, issued.Token, cookieExpiry);

            return Results.Ok(new UserResponse(user.Id, user.UserName!, roles.ToList(), user.MustChangePassword));
        });

        // Rotates the refresh token and mints a fresh access JWT. Web reads/writes cookies; mobile passes
        // and receives the tokens in the body (ADR-0005) — the cookie path never returns the refresh
        // token in the body, preserving HttpOnly.
        group.MapPost("/auth/refresh", async (RefreshRequest? body, UserManager<User> users, JwtTokenService tokens,
            RefreshTokenService refreshTokens, IOptions<RefreshTokenOptions> refreshOptions, HttpRequest request, HttpResponse response) =>
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
            // Re-stamp the forced-change flag from the current DB state so an Admin reset takes effect on
            // the next refresh (issue 0024), even on a live session.
            var (access, _) = tokens.Create(user, roles, rotation.ChainId, user.MustChangePassword);

            if (isCookieFlow)
            {
                var cookieExpiry = CookieExpiry(refreshOptions);
                AuthCookie.SetAccess(response, access, cookieExpiry);
                AuthCookie.SetRefresh(response, rotation.Token!, cookieExpiry);
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
            var username = principal.FindFirstValue(JwtRegisteredClaimNames.PreferredUsername);
            var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            var mustChange = principal.FindFirstValue(JwtTokenService.MustChangePasswordClaim) == "true";
            return Results.Ok(new UserResponse(Guid.Parse(id!), username ?? "", roles, mustChange));
        }).RequireAuthorization();

        // Change-own-password (issue 0024): clears the forced-change flag and revokes the User's other
        // sessions (the 0019 deferral). On the web cookie flow this device is re-established so the User
        // stays signed in here. Allowlisted by the password-change sentinel.
        group.MapPost("/auth/change-password", async (ChangePasswordRequest body, UserManager<User> users,
            JwtTokenService tokens, RefreshTokenService refreshTokens, IOptions<RefreshTokenOptions> refreshOptions,
            ClaimsPrincipal principal, HttpRequest request, HttpResponse response) =>
        {
            var id = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var user = id is null ? null : await users.FindByIdAsync(id);
            if (user is null)
                return Results.Unauthorized();

            var result = await users.ChangePasswordAsync(user, body.CurrentPassword, body.NewPassword);
            if (!result.Succeeded)
                return Results.ValidationProblem(result.Errors.GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));

            if (user.MustChangePassword)
            {
                user.MustChangePassword = false;
                await users.UpdateAsync(user);
            }

            // Revoke every existing session, then re-establish THIS device (ADR-0005).
            await refreshTokens.RevokeAllAsync(user.Id);
            var roles = await users.GetRolesAsync(user);
            var issued = await refreshTokens.IssueAsync(user.Id, DeviceLabel(request));
            var (access, _) = tokens.Create(user, roles, issued.ChainId, mustChangePassword: false);
            var cookieExpiry = CookieExpiry(refreshOptions);
            AuthCookie.SetAccess(response, access, cookieExpiry);
            AuthCookie.SetRefresh(response, issued.Token, cookieExpiry);
            return Results.NoContent();
        }).RequireAuthorization();

        return app;
    }

    /// <summary>Transport-only cookie lifetime on the real wall clock (the durable session is bounded by
    /// the server-side refresh token, not the cookie).</summary>
    private static DateTimeOffset CookieExpiry(IOptions<RefreshTokenOptions> refreshOptions) =>
        DateTimeOffset.UtcNow + refreshOptions.Value.InactivityWindow;

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
