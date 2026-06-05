using Microsoft.AspNetCore.Http;

namespace JournalRecall.Api.Auth;

/// <summary>
/// The first-party tokens are delivered to the web SPA as strict HttpOnly cookies (ADR-0002, ADR-0005)
/// — the browser does zero token handling. The short-lived access JWT rides the access cookie (sent on
/// every request); the long-lived refresh token rides the refresh cookie, path-scoped to the refresh
/// endpoint so it is only ever sent there. Secure is set to match the request scheme so http dev/tests
/// work while production over https stays Secure (cookie-prefix hardening lands in issue 0020).
/// </summary>
public static class AuthCookie
{
    public const string AccessName = "jr_auth";
    public const string RefreshName = "jr_refresh";

    /// <summary>The refresh cookie is scoped to the refresh endpoint so it never rides ordinary requests.</summary>
    public const string RefreshPath = "/api/auth/refresh";

    public static void SetAccess(HttpResponse response, string token, DateTimeOffset expires) =>
        response.Cookies.Append(AccessName, token, AccessOptions(response, expires));

    public static void ClearAccess(HttpResponse response) =>
        response.Cookies.Delete(AccessName, AccessOptions(response, expires: null));

    public static void SetRefresh(HttpResponse response, string token, DateTimeOffset expires) =>
        response.Cookies.Append(RefreshName, token, RefreshOptions(response, expires));

    public static void ClearRefresh(HttpResponse response) =>
        response.Cookies.Delete(RefreshName, RefreshOptions(response, expires: null));

    private static CookieOptions AccessOptions(HttpResponse response, DateTimeOffset? expires) =>
        BaseOptions(response, expires, path: "/");

    private static CookieOptions RefreshOptions(HttpResponse response, DateTimeOffset? expires) =>
        BaseOptions(response, expires, path: RefreshPath);

    private static CookieOptions BaseOptions(HttpResponse response, DateTimeOffset? expires, string path) => new()
    {
        HttpOnly = true,
        Secure = response.HttpContext.Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = path,
        Expires = expires,
    };
}
