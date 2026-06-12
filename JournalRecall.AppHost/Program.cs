var builder = DistributedApplication.CreateBuilder(args);

// Dev orchestration only (ADR-0001). Fixed custom ports so local runs are reproducible and avoid
// common-default collisions: API http 5247 / https 7247, web 4247. The store is file-based SQLite,
// so there is no database container to manage.

var api = builder.AddProject<Projects.JournalRecall_Api>("api")
    .WithHttpEndpoint(port: 5247)
    .WithHttpsEndpoint(port: 7247)
    // Surface a Swagger UI link on the resource in the dashboard. Build an absolute URL from the
    // API endpoint's resolved address — a bare relative "/swagger" resolves against the dashboard
    // origin and 404s. Profile-agnostic (uses whichever http/https endpoint is allocated).
    .WithUrls(context =>
    {
        var endpoint = context.Urls.FirstOrDefault(u =>
            u.Endpoint is not null && Uri.TryCreate(u.Url, UriKind.Absolute, out _));
        if (endpoint is not null && Uri.TryCreate(new Uri(endpoint.Url), "/swagger", out var swaggerUrl))
            context.Urls.Add(new() { Url = swaggerUrl.ToString(), DisplayText = "Swagger UI" });
    });

// The client-only Vite SPA, orchestrated as a pnpm app. In dev the browser talks to a single origin:
// Vite serves /app and proxies /api to the API (see web/vite.config.ts).
builder.AddPnpmApp("web", "../src/JournalRecall.Api/web", "dev")
    .WithReference(api).WaitFor(api)
    .WithHttpEndpoint(port: 4247, env: "PORT")
    .WithExternalHttpEndpoints()
    .WithPnpmPackageInstallation();

builder.Build().Run();
