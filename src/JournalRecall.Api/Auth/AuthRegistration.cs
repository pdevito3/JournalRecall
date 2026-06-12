using System.Text;
using Duende.AspNetCore.Authentication.JwtBearer.DPoP;
using HeimGuard;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Identity;
using JournalRecall.Api.Services;

namespace JournalRecall.Api.Auth;

public static class AuthRegistration
{
    /// <summary>
    /// Wires ASP.NET Core Identity, first-party JWT minting/validation, and authorization. The single
    /// JWT is read from the HttpOnly cookie or an Authorization: Bearer header (ADR-0002).
    /// </summary>
    public static IServiceCollection AddJournalRecallAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey), "Jwt:SigningKey must be configured.")
            .Validate(o => Encoding.UTF8.GetByteCount(o.SigningKey) >= 32, "Jwt:SigningKey must be at least 32 bytes for HMAC-SHA256.")
            .ValidateOnStart();

        services.AddSingleton<JwtTokenService>();

        // Token-endpoint DPoP proof validation (ADR-0014): the first-party half. The resource-server
        // half is the Duende extension wired onto the JwtBearer scheme below.
        services.AddSingleton<DPoPProofValidator>();

        // The shared proof-replay cache (issue 0038/0041): one keyed HybridCache used by BOTH halves —
        // the first-party validator's jti check and the Duende library's replay detection. It is owned
        // under an app key (the first-party half stays Duende-free); the Duende resource-server key is
        // aliased to the very same singleton here, in the one place that's allowed to know both keys, so
        // a multi-instance deployment swaps in a distributed backing on this single registration. The two
        // halves namespace their entries by prefix, so they share the backing, not entries.
        services.AddKeyedHybridCache(HybridDPoPReplayCache.CacheServiceKey);
        services.AddKeyedSingleton<HybridCache>(ServiceProviderKeys.ProofTokenReplayHybridCache,
            (sp, _) => sp.GetRequiredKeyedService<HybridCache>(HybridDPoPReplayCache.CacheServiceKey));
        services.AddSingleton<IDPoPReplayCache, HybridDPoPReplayCache>();

        // Refresh-token rotation & durable sessions (ADR-0005). Scoped over the DbContext; the service
        // is the deep module, the store the thin persistence seam.
        services.AddOptions<RefreshTokenOptions>()
            .Bind(configuration.GetSection(RefreshTokenOptions.SectionName));
        services.AddScoped<IRefreshTokenStore, EfRefreshTokenStore>();
        services.AddScoped<RefreshTokenService>();

        // Operator-controlled registration (issue 0023): app-wide AuthSettings reader/writer.
        services.AddScoped<AuthSettingsService>();

        services.AddIdentityCore<User>(options =>
            {
                // Username is the identity (issue 0027); email is unused. Uniqueness rides Identity's
                // normalized-username index, so no unique-email requirement.
                options.User.RequireUniqueEmail = false;
                // NIST-aligned password policy (PRD-0001): favor length over composition. Identity
                // defaults the four composition rules to true, so turn them off explicitly.
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<JournalRecallDbContext>()
            .AddDefaultTokenProviders(); // password-reset tokens for Admin reset (issue 0024)

        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        var signingKey = string.IsNullOrWhiteSpace(jwt.SigningKey)
            ? null
            : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false; // keep "sub"/"email" claim names as minted
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                };

                // Dual delivery: the Bearer header (set by the base handler) wins; otherwise fall back
                // to the HttpOnly cookie so the web SPA never touches the token.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (string.IsNullOrEmpty(context.Token))
                        {
                            var cookie = context.Request.Cookies[AuthCookie.AccessName];
                            if (!string.IsNullOrEmpty(cookie))
                                context.Token = cookie;
                        }
                        return Task.CompletedTask;
                    },
                    // Resource-side DPoP rejections surface in the auth telemetry (issue 0040): the
                    // Duende events stash the error in HttpContext.Items during TokenValidated; this
                    // inner callback runs before their Challenge half appends the WWW-Authenticate
                    // header. Only the error code/description is recorded — never proof or token bytes.
                    OnChallenge = context =>
                    {
                        if (context.HttpContext.Items.TryGetValue("DPoP-Error", out var error) && error is string)
                        {
                            System.Diagnostics.Activity.Current?.SetTag("auth.dpop.rejected", true);
                            System.Diagnostics.Activity.Current?.SetTag("auth.dpop.failure", error);
                            if (context.HttpContext.Items.TryGetValue("DPoP-ErrorDescription", out var description) && description is string)
                                System.Diagnostics.Activity.Current?.SetTag("auth.dpop.failure_description", description);
                        }
                        else if (context.HttpContext.Items.TryGetValue("Bearer-ErrorDescription", out var downgrade) && downgrade is string)
                        {
                            // A cnf-bound token presented as plain Bearer (the downgrade attempt).
                            System.Diagnostics.Activity.Current?.SetTag("auth.dpop.rejected", true);
                            System.Diagnostics.Activity.Current?.SetTag("auth.dpop.failure", "bearer_downgrade");
                        }
                        return Task.CompletedTask;
                    },
                };
            });

        // Resource-server DPoP enforcement (ADR-0014): the Duende extension wraps the scheme's events —
        // a cnf-bound token must arrive as "Authorization: DPoP <token>" with a fresh matching proof,
        // while AllowBearerTokens keeps plain bearer/cookie tokens (and the OnMessageReceived cookie
        // fallback above) working unchanged. Proofs are ES256 only, matching the token-endpoint half.
        services.ConfigureDPoPTokensForScheme(JwtBearerDefaults.AuthenticationScheme, dpop =>
        {
            dpop.AllowBearerTokens = true;
            // Replay detection rides the shared keyed HybridCache registered above (issue 0038).
            dpop.EnableReplayDetection = true;
            dpop.ProofTokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256];
        });

        services.AddAuthorization();

        // Permission-based gate for the non-journal admin surface (HeimGuard). Roles map to
        // permissions in UserPolicyHandler; MapAuthorizationPolicies() makes each permission usable
        // as an authorization policy name (e.g. .RequireAuthorization(Permissions.CanAccessAdmin)).
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddHeimGuard<UserPolicyHandler>()
            .AutomaticallyCheckPermissions()
            .MapAuthorizationPolicies();

        // Seed Admin/Member roles at startup (after migrations).
        services.AddHostedService<RoleSeederHostedService>();

        return services;
    }
}
