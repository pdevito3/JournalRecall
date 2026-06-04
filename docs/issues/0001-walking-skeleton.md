# 0001 — Walking skeleton

**Phase:** 0 · **Type:** AFK · **Status:** todo · **Realizes:** ADR-0001

## What to build

The thinnest end-to-end path through the whole stack, single origin. A compiling .NET solution
(`JournalRecall.AI`, `JournalRecall.AI.EntityFrameworkCore`, `JournalRecall.Api`, plus test
projects) that boots and serves a placeholder React app at `/app` and a health endpoint at `/api`,
backed by file-based SQLite. Includes the "coming soon" Chat/RAG placeholder page and baseline
observability so later slices inherit them.

- Vite SPA under `JournalRecall.Api/web/` — TanStack Router (client mode) + TanStack Query + React
  Aria + Tailwind, **no** TanStack Start. Builds to `wwwroot/app`; ASP.NET serves `/app/*` with SPA
  fallback to `/app/index.html`. Dev: Vite proxies `/api` → the .NET app (one origin, no CORS).
- `BaseEntity`, `JournalRecallDbContext` on file-based SQLite, an initial migration applied at
  startup.
- Baseline telemetry: Serilog request logging + OpenTelemetry bootstrap (so AI-lifecycle spans in
  #0017 just extend it).
- Chat/RAG route renders a "coming soon" placeholder with a nav entry.
- Aspire AppHost for dev orchestration only (not production).

## Acceptance criteria

- [ ] `dotnet build` succeeds for the solution; `dotnet test` runs with one pure test and one
      DI-wiring test green.
- [ ] Running the app serves the built SPA at `/app` and `GET /api/health` returns 200 **from the
      same origin** (verified via the Vite dev proxy in dev).
- [ ] The SQLite `.db` file is created and the initial migration is applied on first run.
- [ ] Navigating to the Chat route shows the "coming soon" placeholder.
- [ ] A request to `/api/health` produces a structured Serilog log line and an OpenTelemetry trace.

## Blocked by

None — can start immediately.
