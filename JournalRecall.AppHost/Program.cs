var builder = DistributedApplication.CreateBuilder(args);

// Dev orchestration only (ADR-0001). Fixed custom ports so local runs are reproducible and avoid
// common-default collisions: API http 5247 / https 7247, web 4247. The store is file-based SQLite,
// so there is no database container to manage.

var api = builder.AddProject<Projects.JournalRecall_Api>("api")
    .WithHttpEndpoint(port: 5247)
    .WithHttpsEndpoint(port: 7247);

// The client-only Vite SPA, orchestrated as a pnpm app. In dev the browser talks to a single origin:
// Vite serves /app and proxies /api to the API (see web/vite.config.ts).
builder.AddPnpmApp("web", "../src/JournalRecall.Api/web", "dev")
    .WithReference(api).WaitFor(api)
    .WithHttpEndpoint(port: 4247, env: "PORT")
    .WithExternalHttpEndpoints()
    .WithPnpmPackageInstallation();

builder.Build().Run();
