namespace JournalRecall.Api.Auth;

/// <summary>
/// Defense-in-depth CSRF protection (ADR-0005), layered on <c>SameSite=Strict</c> cookies: every
/// state-changing <c>/api</c> request must carry a custom <c>X-CSRF</c> header. A browser cannot set a
/// custom header on a cross-origin request without a CORS preflight we never approve, so a forged
/// cross-site POST — which would otherwise ride the auth cookie — is rejected with 403. Safe (read-only)
/// methods are unaffected.
/// </summary>
public sealed class CsrfMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-CSRF";

    private static readonly HashSet<string> SafeMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS", "TRACE" };

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (request.Path.StartsWithSegments("/api")
            && !SafeMethods.Contains(request.Method)
            && !request.Headers.ContainsKey(HeaderName))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(context);
    }
}
