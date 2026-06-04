using Microsoft.AspNetCore.Http;

namespace JournalRecall.Api.Auth;

/// <summary>
/// The first-party JWT is delivered to the web SPA as a strict HttpOnly cookie (ADR-0002) — the
/// browser does zero token handling. Secure is set to match the request scheme so http dev/tests work
/// while production over https stays Secure.
/// </summary>
public static class AuthCookie
{
    public const string Name = "jr_auth";

    public static void Set(HttpResponse response, string token, DateTimeOffset expires) =>
        response.Cookies.Append(Name, token, Options(response, expires));

    public static void Clear(HttpResponse response) =>
        response.Cookies.Delete(Name, Options(response, expires: null));

    private static CookieOptions Options(HttpResponse response, DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = response.HttpContext.Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        Expires = expires,
    };
}
