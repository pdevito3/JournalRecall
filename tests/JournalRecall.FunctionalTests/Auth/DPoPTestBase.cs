using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Auth;

/// <summary>
/// Shared plumbing for the DPoP bearer flow (ADR-0014): a cookie-less https client, proof-carrying
/// login/refresh, and bound resource requests. Mirrors what the documented mobile contract prescribes
/// (docs/mobile/dpop-proof-contract.md), so these tests double as the contract's verification.
/// </summary>
public abstract class DPoPTestBase(WebTestFixture fixture) : AuthTestBase(fixture)
{
    protected const string DPoPHeader = "DPoP";

    protected sealed record TokenResponse(string AccessToken, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

    /// <summary>A cookie-less client, the bearer-client posture (mirrors the mobile flow). The https base
    /// matters here: a fresh WebApplicationFactoryClientOptions defaults to http://localhost, and DPoP htu
    /// validation compares against the scheme the server actually saw.</summary>
    protected HttpClient BearerClient() =>
        RealAuth.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            BaseAddress = new Uri("https://localhost"),
        });

    /// <summary>Registers a fresh User, then logs in with a DPoP proof from <paramref name="key"/>.</summary>
    protected async Task<HttpResponseMessage> DPoPLogin(HttpClient client, TestDPoPKey key, Credentials? creds = null)
    {
        creds ??= NewUser();
        await client.PostAsJsonAsync(ApiRoutes.Auth.Register, creds);

        var login = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Auth.Login)
        {
            Content = JsonContent.Create(creds),
        };
        login.Headers.Add(DPoPHeader, key.CreateProof("POST", "https://localhost" + ApiRoutes.Auth.Login));
        return await client.SendAsync(login);
    }

    /// <summary>Register + DPoP login, unwrapped to the token pair.</summary>
    protected async Task<TokenResponse> DPoPSession(HttpClient client, TestDPoPKey key, Credentials? creds = null) =>
        (await (await DPoPLogin(client, key, creds)).ReadJsonAsync<TokenResponse>())!;

    /// <summary>A request authorized as <c>DPoP &lt;token&gt;</c>, optionally carrying a proof header.</summary>
    protected static HttpRequestMessage BoundRequest(HttpMethod method, string route, string accessToken, string? proof)
    {
        var request = new HttpRequestMessage(method, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("DPoP", accessToken);
        if (proof is not null)
            request.Headers.Add(DPoPHeader, proof);
        return request;
    }

    /// <summary>A bound request with a fresh, correctly-shaped proof (htm/htu/ath) from <paramref name="key"/>.</summary>
    protected static HttpRequestMessage BoundRequest(HttpMethod method, string route, string accessToken, TestDPoPKey key) =>
        BoundRequest(method, route, accessToken,
            key.CreateProof(method.Method, "https://localhost" + route, accessToken: accessToken));

    /// <summary>Posts to /api/auth/refresh in the bearer body flow, optionally with a proof.</summary>
    protected async Task<HttpResponseMessage> Refresh(HttpClient client, string refreshToken, TestDPoPKey? proofKey)
    {
        var refresh = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Auth.Refresh)
        {
            Content = JsonContent.Create(new { refreshToken }),
        };
        if (proofKey is not null)
            refresh.Headers.Add(DPoPHeader, proofKey.CreateProof("POST", "https://localhost" + ApiRoutes.Auth.Refresh));
        return await client.SendAsync(refresh);
    }
}
