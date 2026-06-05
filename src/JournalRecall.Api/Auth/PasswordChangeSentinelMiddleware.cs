namespace JournalRecall.Api.Auth;

/// <summary>
/// Forced-password-change sentinel (issue 0024, PRD-0001): while a User carries a temporary password
/// (the <see cref="JwtTokenService.MustChangePasswordClaim"/> on their access token), every <c>/api</c>
/// call is rejected with <c>403 password_change_required</c> except a small allowlist — change-own-
/// password, refresh, logout, <c>/me</c>, and the public auth config. The SPA reads <c>/me</c>, sees the
/// flag, and confines the User to the set-new-password screen. The flag is re-stamped on refresh from the
/// DB and cleared once the User sets their own password.
/// </summary>
public sealed class PasswordChangeSentinelMiddleware(RequestDelegate next)
{
    public const string Reason = "password_change_required";

    // Endpoints a forced-change User may still reach (exact paths).
    private static readonly HashSet<string> Allowlist = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/change-password",
        "/api/auth/refresh",
        "/api/auth/logout",
        "/api/me",
        "/api/auth/config",
    };

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/api")
            && context.User.FindFirst(JwtTokenService.MustChangePasswordClaim)?.Value == "true"
            && !(path.Value is { } value && Allowlist.Contains(value.TrimEnd('/'))))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($"{{\"error\":\"{Reason}\"}}");
            return;
        }

        await next(context);
    }
}
