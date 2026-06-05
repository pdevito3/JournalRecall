using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using JournalRecall.AI.OpenAI;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Admin;
using JournalRecall.Api.Domain.Admin.Dtos;
using JournalRecall.Api.Domain.Identity;

namespace JournalRecall.Api.Auth;

public static class AdminEndpoints
{
    /// <summary>
    /// The non-journal admin surface (issue 0016, ADR-0002): user management + the app-wide AI provider
    /// config. Gated by the admin permission (HeimGuard). It exposes <strong>no</strong> journal data for
    /// any user — there is deliberately no Session/Summary/Correction-reading endpoint here, upholding the
    /// Privacy invariant that no one, Admin included, can read another User's journal.
    /// </summary>
    public static IEndpointRouteBuilder MapAdmin(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization(Permissions.CanAccessAdmin);

        group.MapGet("/ping", () => Results.Ok(new { ok = true }));

        // --- User management ---

        group.MapGet("/users", async (UserManager<User> users) =>
        {
            var all = await users.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync();
            var dtos = new List<AdminUserDto>(all.Count);
            foreach (var user in all)
                dtos.Add(new AdminUserDto(user.Id, user.Email!, (await users.GetRolesAsync(user)).ToList(), user.IsDisabled));
            return Results.Ok(dtos);
        });

        group.MapPost("/users", async (CreateUserRequest body, UserManager<User> users) =>
        {
            var role = body.Role == Roles.Admin ? Roles.Admin : Roles.Member;
            var user = new User { UserName = body.Email, Email = body.Email };
            var result = await users.CreateAsync(user, body.Password);
            if (!result.Succeeded)
                return Results.ValidationProblem(result.Errors.GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));

            await users.AddToRoleAsync(user, role);
            return Results.Created($"/api/admin/users/{user.Id}",
                new AdminUserDto(user.Id, user.Email!, [role], user.IsDisabled));
        });

        group.MapPut("/users/{id:guid}/role", async (Guid id, SetRoleRequest body, UserManager<User> users) =>
        {
            if (!Roles.All.Contains(body.Role))
                return Results.Problem("Unknown role.", statusCode: StatusCodes.Status400BadRequest);

            var user = await users.FindByIdAsync(id.ToString());
            if (user is null)
                return Results.NotFound();

            await users.RemoveFromRolesAsync(user, await users.GetRolesAsync(user));
            await users.AddToRoleAsync(user, body.Role);
            return Results.NoContent();
        });

        group.MapPost("/users/{id:guid}/disable", (Guid id, UserManager<User> users, RefreshTokenService refreshTokens) =>
            SetDisabled(id, true, users, refreshTokens));
        group.MapPost("/users/{id:guid}/enable", (Guid id, UserManager<User> users, RefreshTokenService refreshTokens) =>
            SetDisabled(id, false, users, refreshTokens));

        // --- Operator-controlled registration (issue 0023) ---

        group.MapGet("/registration", async (AuthSettingsService authSettings) =>
            Results.Ok(new RegistrationSettingsDto(await authSettings.SelfRegistrationEnabledAsync())));

        group.MapPut("/registration", async (RegistrationSettingsRequest body, AuthSettingsService authSettings) =>
        {
            await authSettings.SetSelfRegistrationAsync(body.SelfRegistrationEnabled);
            return Results.NoContent();
        });

        // --- App-wide AI provider config ---

        group.MapGet("/ai-provider", async (JournalRecallDbContext db) =>
        {
            var settings = await db.AiProviderSettings.AsNoTracking().FirstOrDefaultAsync();
            return Results.Ok(settings is null
                ? new AiProviderDto(ChatProvider.OpenAI.ToString(), null, "", false)
                : new AiProviderDto(settings.Provider.ToString(), settings.Endpoint, settings.Model, settings.ApiKey is not null));
        });

        group.MapPut("/ai-provider", async (AiProviderRequest body, JournalRecallDbContext db) =>
        {
            if (!Enum.TryParse<ChatProvider>(body.Provider, ignoreCase: true, out var provider))
                return Results.Problem("Unknown provider.", statusCode: StatusCodes.Status400BadRequest);

            var settings = await db.AiProviderSettings.FirstOrDefaultAsync();
            if (settings is null)
            {
                settings = AiProviderSettings.Create();
                db.AiProviderSettings.Add(settings);
            }

            // A blank key means "leave the stored secret as-is" (the GET never returns it to pre-fill).
            var apiKey = string.IsNullOrWhiteSpace(body.ApiKey) ? settings.ApiKey : body.ApiKey;
            settings.Update(provider, body.Endpoint, apiKey, body.Model);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }

    private static async Task<IResult> SetDisabled(Guid id, bool disabled, UserManager<User> users, RefreshTokenService refreshTokens)
    {
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null)
            return Results.NotFound();

        user.IsDisabled = disabled;
        await users.UpdateAsync(user);

        // Disabling revokes all of the User's sessions so a disabled User is locked out everywhere, not
        // just at next login — bounded by the short access-token lifetime for any live token (ADR-0005).
        if (disabled)
            await refreshTokens.RevokeAllAsync(id);

        return Results.NoContent();
    }
}
