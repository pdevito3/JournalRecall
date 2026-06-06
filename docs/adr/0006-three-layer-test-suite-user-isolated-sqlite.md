# Three-layer test suite with User-isolated shared SQLite

## Status

accepted

## Context & decision

The original `JournalRecall.Api.Tests` lumped pure-domain unit tests, in-process feature tests, and
full-HTTP tests into one project. With no layer to reach for, almost everything became a heavyweight
`WebApplicationFactory` test — each re-booting its own throwaway SQLite file and re-running
migrations — even when no HTTP was involved. There was no fast middle layer, no reusable test-data
factories, and the AI fakes were trapped inside the test project.

We adopt a deliberate **three-layer** architecture modelled on the PeakLimsApi pattern
(`UnitTests` / `IntegrationTests` / `FunctionalTests` / `SharedTestHelpers`) but adapted to
JournalRecall's substrate:

- **Unit** — pure domain and isolated logic; no host, no DB. Runs in parallel.
- **Integration** — the best bang for buck: a real DI scope, real SQLite, and the real MediatR slice
  driven in-process via `SendAsync` with no HTTP. **A User is the isolation boundary.**
- **Functional** — anything needing the web host: real auth, CSRF, the access gate, status codes,
  JSON shapes, and SSE.
- **SharedTestHelpers** — `Fake{Aggregate}Builder` domain builders (Bogus-backed), `AutoFaker<T>`
  (Soenneker) for DTOs/requests, the script-controllable `ScriptableChatClient`, a `FakeUserBuilder`,
  and a `ClaimsPrincipal` helper.

The keystone divergence from PeakLims is the **integration-test isolation model**. PeakLims uses
Testcontainers Postgres + Respawn to reset between tests. JournalRecall instead:

- Uses **one shared SQLite file** for the whole integration assembly, created by a collection fixture,
  with **real migrations run once** at fixture init (not `EnsureCreated` — we keep the "migrations boot
  end-to-end" coverage).
- Performs **no reset between tests**. Each `TestingServiceScope` mints a **fresh random User** (direct
  `DbContext` insert, bypassing `UserManager`/Identity) and sets a `ClaimsPrincipal` (with `sub` = the
  seeded User's id) on a mocked `IHttpContextAccessor`, so the per-User tenant query filter isolates all
  user-scoped data and list/count queries naturally on the shared DB.
- **Bypasses HTTP** — integration tests resolve a DI scope and `SendAsync` MediatR directly, so CSRF /
  access-gate / auth middleware never runs. `ICurrentUserService` resolves identity exactly as in
  production (the `JournalRecallDbContext` captures the current user id at construction, so the user is
  set on the singleton accessor before the context is resolved). HeimGuard is permitted-by-default with
  a `SetUserNotPermitted(...)` escape hatch.
- Sends **truly app-global tests** (registration policy, app settings — not User-scoped) to the
  functional layer or a dedicated serial `GlobalState` collection, never the shared integration layer.

Functional tests default to **real auth** (`CreateAuthenticatedClientAsync()` runs the genuine
register→login flow, carrying the real cookie/bearer + `X-CSRF` header). A **fake-auth scheme** is
opt-in and registered **only** in a dedicated `FakeAuthWebApplicationFactory` (never in `Program`); it
skips only token issuance, so the request still flows through CSRF and the access gate. Auth-behavior
tests always use real auth.

## Considered options

- **Keep one mixed project** — status quo. Rejected: no layering, everything pays full-HTTP cost.
- **Testcontainers Postgres + Respawn (the PeakLims substrate verbatim)** — rejected: this app is
  file-based SQLite (ADR-0001), Respawn has poor SQLite support, and the User boundary already gives
  natural isolation without a reset step or a Docker dependency in the test loop.
- **`EnsureCreated` per integration run instead of migrations** — rejected: it would drop the coverage
  that the real migrations apply cleanly end-to-end on a fresh database.
- **A fake-auth bypass at the integration layer** — rejected as unnecessary; integration sets a
  `ClaimsPrincipal` on the mocked accessor directly.

## Consequences

- Stack choices follow the repo: **Shouldly** (not FluentAssertions, now paid at v8),
  **`Soenneker.Utils.AutoBogus`** on plain **Bogus**, **NSubstitute**.
- Integration and functional each run as a single serial collection (shared SQLite file / process-global
  host state); unit runs in parallel; cross-assembly parallelism stays on.
- `JournalRecall.AI.Tests` is out of scope and untouched — it tests a different assembly and has its own
  fakes.

See [PRD-0003](../prd/0003-three-layer-test-suite-builders-fakers.md) and `tests/README.md` for the
day-to-day decision tree. Auth architecture per [ADR-0002](0002-cookie-wrapped-jwt-auth.md) and
[ADR-0005](0005-refresh-token-rotation-and-cookie-hardening.md); revisions per
[ADR-0003](0003-append-only-content-revisions.md); SQLite per [ADR-0001](0001-vite-spa-embedded-in-dotnet.md).
