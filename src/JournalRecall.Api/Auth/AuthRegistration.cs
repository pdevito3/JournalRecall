using System.Text;
using HeimGuard;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
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
                options.User.RequireUniqueEmail = true;
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
                };
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
