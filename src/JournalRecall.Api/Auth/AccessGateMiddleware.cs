using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Domain.Identity;

namespace JournalRecall.Api.Auth;

/// <summary>
/// Server-side access gate (issue 0022, PRD-0001): a signed-out visitor who deep-links to a protected
/// SPA route is redirected <em>before the app loads</em>, so they never receive the shell for content
/// they can't see. It guards only SPA navigations under <c>/app</c> (the API enforces its own auth and
/// static assets serve freely), allows the public client routes (login / register / setup), and 302s
/// everyone else to <c>/app/setup</c> on a fresh instance, else <c>/app/login</c>.
///
/// "Signed out" is judged by the presence of the access cookie, not by JWT validity: the durable-session
/// model (ADR-0005) keeps the long-lived access cookie around while its short JWT is silently rotated, so
/// a returning User with an expired JWT must still get the shell (the client then refreshes). The JWT is
/// the real security boundary at the API; this gate is a routing optimization for the anonymous case.
/// </summary>
public sealed class AccessGateMiddleware(RequestDelegate next)
{
    private const string AppBase = "/app";

    // Public SPA routes a signed-out visitor may always reach (under the /app basepath). The register
    // route is conditionally public — only when self-registration is enabled (issue 0023).
    private static readonly HashSet<string> PublicRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/app", "/app/login", "/app/setup",
    };

    private const string RegisterRoute = "/app/register";

    public async Task InvokeAsync(HttpContext context, UserManager<User> users, AuthSettingsService authSettings)
    {
        var path = context.Request.Path;

        // Only gate SPA *navigations*: requests under /app with no file extension (assets have one and
        // are served by the static-file middleware; the SPA fallback uses the same :nonfile rule).
        if (!path.StartsWithSegments(AppBase)
            || path.Value is { } value && Path.HasExtension(value)
            || context.Request.Cookies.ContainsKey(AuthCookie.AccessName)
            || IsPublicRoute(path)
            || (IsRegisterRoute(path) && await authSettings.SelfRegistrationEnabledAsync()))
        {
            await next(context);
            return;
        }

        var needsSetup = !await users.Users.AnyAsync();
        context.Response.Redirect(needsSetup ? "/app/setup" : "/app/login");
    }

    private static bool IsRegisterRoute(PathString path) =>
        string.Equals(path.Value?.TrimEnd('/'), RegisterRoute, StringComparison.OrdinalIgnoreCase);

    private static bool IsPublicRoute(PathString path) =>
        path.Value is { } value && PublicRoutes.Contains(value.TrimEnd('/') is "" ? "/app" : value.TrimEnd('/'));
}
