using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace JournalRecall.FunctionalTests;

/// <summary>
/// An opt-in factory that registers a <b>test-only fake authentication scheme</b> as the default
/// (PRD-0003). It exists <em>only here</em> — never in <c>Program</c>. A caller declares who they are via
/// <c>client.AsUser(...)</c>/<c>AsAdmin()</c> headers; the handler builds a principal shaped exactly like a
/// validated one (<c>sub</c> + role claims). It skips <b>only</b> token issuance — the request still flows
/// through CSRF and the access gate, so the bypass never routes around middleware production runs.
/// </summary>
public sealed class FakeAuthWebApplicationFactory : FunctionalWebApplicationFactory
{
    public const string UserIdHeader = "X-Test-UserId";
    public const string RolesHeader = "X-Test-Roles";

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        // Runs after Program's AddAuthentication(JwtBearer), so this resets the default scheme to the fake.
        services.AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
    }

    /// <summary>
    /// Authenticates each request from the <c>X-Test-*</c> headers an <c>AsUser</c>/<c>AsAdmin</c> client
    /// attaches. No headers ⇒ no result, so anonymous behavior is still testable.
    /// </summary>
    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "TestAuth";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserIdHeader, out var userId) || string.IsNullOrEmpty(userId))
                return Task.FromResult(AuthenticateResult.NoResult());

            var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, userId!) };
            if (Request.Headers.TryGetValue(RolesHeader, out var roles))
                foreach (var role in roles.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    claims.Add(new Claim(ClaimTypes.Role, role));

            var identity = new ClaimsIdentity(claims, SchemeName, JwtRegisteredClaimNames.Sub, ClaimTypes.Role);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
