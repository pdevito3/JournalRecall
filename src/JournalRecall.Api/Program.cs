using Serilog;
using JournalRecall.Api.Auth;
using JournalRecall.Api.Domain.Corrections;
using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Domain.Users;
using JournalRecall.Api.Extensions;

// Stage 1: a bootstrap logger that captures anything logged before the host (and the real Serilog
// config) is built — including startup failures (serilog-aspnetcore two-stage initialization).
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting JournalRecall API");

    var builder = WebApplication.CreateBuilder(args);

    builder.AddServiceDefaults(); // OpenTelemetry, health checks, resilience, service discovery (HostExtensions)
    builder.ConfigureServices();  // Stage 2: reconfigures Serilog from config + DI

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    app.UseAuthentication();
    app.UseAuthorization();

    // Health probe under /api so it shares the SPA's single origin (ADR-0001). Traced + logged so
    // issue 0017's AI spans extend the same pipeline.
    app.MapHealthChecks("/api/health");

    // Auth: register/login (HttpOnly cookie) + /api/me (ADR-0002).
    app.MapAuth();

    // Admin surface, gated by the admin permission (HeimGuard).
    app.MapAdmin();

    // Session core: create, autosave Draft, read, timeline (per-user via the global query filter).
    app.MapSessions();

    // Per-user settings (timezone for journaling-day derivation).
    app.MapUserSettings();

    // Per-user Corrections that fix mis-dictations during Cleanup.
    app.MapCorrections();

    // Serve the built Vite SPA from wwwroot/app at /app/*, with a fallback to its index.html so
    // client-side routes (e.g. /app/chat) deep-link. "/" redirects into the app.
    app.UseStaticFiles();
    app.MapGet("/", () => Results.Redirect("/app"));
    app.MapFallbackToFile("/app/{*path:nonfile}", "app/index.html");

    app.Run();
}
// Let the hosting infrastructure's control-flow exceptions propagate — HostAbortedException and
// WebApplicationFactory's StopTheHostException both originate from Microsoft.Extensions.Hosting and
// must reach the test host. Only genuine startup failures are fatal (serilog-aspnetcore guidance).
catch (Exception ex) when (ex.Source is not "Microsoft.Extensions.Hosting")
{
    Log.Fatal(ex, "JournalRecall API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program;
