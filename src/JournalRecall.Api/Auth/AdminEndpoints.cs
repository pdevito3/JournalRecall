namespace JournalRecall.Api.Auth;

public static class AdminEndpoints
{
    /// <summary>
    /// The non-journal admin surface. For now a single stub gated by the admin permission to prove the
    /// HeimGuard gate; user management and app settings land in issue 0016.
    /// </summary>
    public static IEndpointRouteBuilder MapAdmin(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/ping", () => Results.Ok(new { ok = true }))
            .RequireAuthorization(Permissions.CanAccessAdmin);

        return app;
    }
}
