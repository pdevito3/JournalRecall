using Microsoft.AspNetCore.Http;

namespace JournalRecall.Api.Auth;

/// <summary>
/// The first-party tokens are delivered to the web SPA as strict, prefix-hardened HttpOnly cookies
/// (ADR-0002, ADR-0005) — the browser does zero token handling. The short-lived access JWT rides the
/// <c>__Host-jr_auth</c> cookie (sent on every request, <c>Path=/</c>, no <c>Domain</c>); the long-lived
/// refresh token rides the <c>__Secure-jr_refresh</c> cookie, path-scoped to the refresh endpoint so it
/// is only ever sent there (the <c>__Host-</c> prefix can't be used with a non-<c>/</c> path). Both are
/// <c>HttpOnly</c>, <c>SameSite=Strict</c>, and <c>Secure</c> unconditionally — the names no longer
/// depend on the request scheme, so <strong>HTTPS is required</strong> (issue 0020). Aspire serves over
/// TLS in dev, so this is operationally a no-op.
/// </summary>
public static class AuthCookie
{
    // __Host- pins the cookie to the exact host with Path=/ and no Domain (no subdomain injection).
    public const string AccessName = "__Host-jr_auth";
    // __Secure- (not __Host-) because the refresh cookie needs a non-"/" path, which __Host- forbids.
    public const string RefreshName = "__Secure-jr_refresh";

    /// <summary>The refresh cookie is scoped to the refresh endpoint so it never rides ordinary requests.</summary>
    public const string RefreshPath = "/api/auth/refresh";

    public static void SetAccess(HttpResponse response, string token, DateTimeOffset expires) =>
        response.Cookies.Append(AccessName, token, AccessOptions(expires));

    public static void ClearAccess(HttpResponse response) =>
        response.Cookies.Delete(AccessName, AccessOptions(expires: null));

    public static void SetRefresh(HttpResponse response, string token, DateTimeOffset expires) =>
        response.Cookies.Append(RefreshName, token, RefreshOptions(expires));

    public static void ClearRefresh(HttpResponse response) =>
        response.Cookies.Delete(RefreshName, RefreshOptions(expires: null));

    private static CookieOptions AccessOptions(DateTimeOffset? expires) => BaseOptions(expires, path: "/");

    private static CookieOptions RefreshOptions(DateTimeOffset? expires) => BaseOptions(expires, path: RefreshPath);

    private static CookieOptions BaseOptions(DateTimeOffset? expires, string path) => new()
    {
        HttpOnly = true,
        Secure = true, // unconditional — the cookie prefixes require it (HTTPS in dev via Aspire)
        SameSite = SameSiteMode.Strict,
        Path = path,
        Expires = expires,
    };
}
