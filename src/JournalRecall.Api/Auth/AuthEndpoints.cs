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
            RefreshTokenService refreshTokens, DPoPProofValidator proofValidator,
            IOptions<RefreshTokenOptions> refreshOptions, HttpRequest request, HttpResponse response) =>
        {
            var user = await users.FindByNameAsync(body.Username);
            if (user is null || !await users.CheckPasswordAsync(user, body.Password))
                return Results.Unauthorized();

            // A disabled account cannot log in (issue 0016) — checked after the password so it doesn't
            // reveal which accounts exist.
            if (user.IsDisabled)
                return Results.Unauthorized();

            // DPoP is per-session opt-in, driven by the client presenting a proof (ADR-0014): a valid
            // proof binds the minted token to the proof key; no header means today's unbound behavior.
            string? boundKeyThumbprint = null;
            if (request.Headers.ContainsKey(DPoPProofValidator.HeaderName))
            {
                var proof = await ValidateProof(proofValidator, request);
                if (!proof.Succeeded)
                    return DPoPChallenge(response, proof.Failure);
                boundKeyThumbprint = proof.Thumbprint;
            }

            var roles = await users.GetRolesAsync(user);

            // Mint a refresh token for this device first so the access JWT can carry its chain id
            // (ADR-0005). A DPoP login binds the chain to the same device key as the access token, so a
            // stolen refresh token cannot mint new tokens without the key (ADR-0014 / issue 0039).
            var issued = await refreshTokens.IssueAsync(user.Id, DeviceLabel(request), boundKeyThumbprint);
            var (token, _) = tokens.Create(user, roles, issued.ChainId, user.MustChangePassword, boundKeyThumbprint);

            // A bound session is a bearer client by definition: the tokens go in the body, never in the
            // auth cookies — the cookie fallback presents tokens as plain bearer, which the resource
            // server rejects for cnf-bound tokens (and the web flow stays deliberately unbound).
            if (boundKeyThumbprint is not null)
                return Results.Ok(new TokenResponse(token, issued.Token, issued.ExpiresAt));

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
            RefreshTokenService refreshTokens, DPoPProofValidator proofValidator,
            IOptions<RefreshTokenOptions> refreshOptions, HttpRequest request, HttpResponse response) =>
        {
            var fromCookie = request.Cookies[AuthCookie.RefreshName];
            var isCookieFlow = !string.IsNullOrEmpty(fromCookie);
            var presented = fromCookie ?? body?.RefreshToken;
            if (string.IsNullOrEmpty(presented))
                return Results.Unauthorized();

            // Look the presented token up before touching the DPoP proof (issue 0042). The probe is
            // side-effect-free, so an invalid proof still never burns the rotation — but an anonymous
            // caller with a garbage token can no longer force an ES256 verify and a replay-cache write
            // against a token that was never issued.
            if (!await refreshTokens.ExistsAsync(presented))
            {
                if (isCookieFlow) ClearAuthCookies(response);
                return Results.Unauthorized();
            }

            // An invalid proof is rejected before the rotation so it never burns the presented token;
            // the service then enforces the key↔chain match for bound chains — a missing or wrong-key
            // proof revokes the chain as suspected theft (ADR-0014 / issue 0039).
            string? presentedKeyThumbprint = null;
            if (request.Headers.ContainsKey(DPoPProofValidator.HeaderName))
            {
                var proof = await ValidateProof(proofValidator, request);
                if (!proof.Succeeded)
                    return DPoPChallenge(response, proof.Failure);
                presentedKeyThumbprint = proof.Thumbprint;
            }

            var rotation = await refreshTokens.RotateAsync(presented, DeviceLabel(request), presentedKeyThumbprint);
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
            // the next refresh (issue 0024), even on a live session. A bound chain mints a bound access
            // token — the binding carries across the whole session, not just the first token (ADR-0014).
            var (access, _) = tokens.Create(user, roles, rotation.ChainId, user.MustChangePassword, rotation.BoundKeyThumbprint);

            // A bound chain is a bearer session: its tokens go in the body, never the auth cookies. A
            // cnf-bound access token written to the cookie would be presented as plain Bearer on the next
            // cookie-fallback request and rejected by the resource server, wedging the session while each
            // cookie refresh re-mints bound cookies (issue 0042). So only an unbound chain rides cookies;
            // a bound chain — even one that somehow arrived via the refresh cookie — returns the body and
            // clears any stale cookies so no bound token lingers to wedge a follow-up request.
            if (isCookieFlow && rotation.BoundKeyThumbprint is null)
            {
                var cookieExpiry = CookieExpiry(refreshOptions);
                AuthCookie.SetAccess(response, access, cookieExpiry);
                AuthCookie.SetRefresh(response, rotation.Token!, cookieExpiry);
                return Results.Ok();
            }

            if (isCookieFlow) ClearAuthCookies(response);
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

            // Revoke every existing session, then re-establish THIS device (ADR-0005). A bound bearer
            // caller (its access token carries cnf — possession of the device key was just proven on this
            // request) is re-established as a bound chain on the SAME key, mirroring the login split
            // (ADR-0014 / issue 0043): the replacement tokens go in the body, no cookies, so the binding
            // isn't silently dropped and no orphan unbound chain is minted. The web cookie flow is
            // untouched — unbound chain, cookies, 204.
            await refreshTokens.RevokeAllAsync(user.Id);
            var roles = await users.GetRolesAsync(user);
            var boundKeyThumbprint = BoundKeyThumbprint(principal);
            var issued = await refreshTokens.IssueAsync(user.Id, DeviceLabel(request), boundKeyThumbprint);
            var (access, _) = tokens.Create(user, roles, issued.ChainId, mustChangePassword: false, boundKeyThumbprint);

            if (boundKeyThumbprint is not null)
                return Results.Ok(new TokenResponse(access, issued.Token, issued.ExpiresAt));

            var cookieExpiry = CookieExpiry(refreshOptions);
            AuthCookie.SetAccess(response, access, cookieExpiry);
            AuthCookie.SetRefresh(response, issued.Token, cookieExpiry);
            return Results.NoContent();
        }).RequireAuthorization();

        return app;
    }

    /// <summary>The bound device-key thumbprint of an authenticated bearer caller, read from the access
    /// token's RFC 9449 <c>cnf: { jkt }</c> claim (ADR-0014). Null for an unbound (web cookie) caller.
    /// Possession of the key was already proven by the resource-server DPoP handler on this request, so a
    /// bound endpoint can re-bind to it without re-validating a proof.</summary>
    private static string? BoundKeyThumbprint(ClaimsPrincipal principal)
    {
        var cnf = principal.FindFirstValue(JwtTokenService.ConfirmationClaim);
        if (string.IsNullOrEmpty(cnf))
            return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(cnf);
            return doc.RootElement.TryGetProperty("jkt", out var jkt) ? jkt.GetString() : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>Runs the token-endpoint proof check (ADR-0014) against the actual request method + URL.
    /// Multiple DPoP headers are a malformed request per RFC 9449.</summary>
    private static async Task<DPoPProofValidator.ProofValidation> ValidateProof(
        DPoPProofValidator proofValidator, HttpRequest request)
    {
        var headers = request.Headers[DPoPProofValidator.HeaderName];
        if (headers.Count != 1)
            return DPoPProofValidator.ProofValidation.Fail(DPoPProofFailure.MalformedProof);

        var url = $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}";
        return await proofValidator.ValidateAsync(headers.ToString(), request.Method, url);
    }

    /// <summary>401 with the RFC 9449 token-endpoint rejection: a DPoP <c>WWW-Authenticate</c> challenge
    /// naming <c>invalid_dpop_proof</c> and the typed failure, so a client can tell a retryable freshness
    /// failure from a hard one. The rejection surfaces in the auth telemetry (issue 0040) as span tags +
    /// a structured log event carrying only the typed failure — never token or proof contents.</summary>
    private static IResult DPoPChallenge(HttpResponse response, DPoPProofFailure failure)
    {
        var context = response.HttpContext;
        System.Diagnostics.Activity.Current?.SetTag("auth.dpop.rejected", true);
        System.Diagnostics.Activity.Current?.SetTag("auth.dpop.failure", failure.ToString());
        context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(AuthEndpoints))
            .LogWarning("DPoP proof rejected at {Path}: {DPoPFailure}", context.Request.Path, failure);

        response.Headers.WWWAuthenticate = $"DPoP error=\"invalid_dpop_proof\", error_description=\"{failure}\"";
        return Results.Unauthorized();
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
