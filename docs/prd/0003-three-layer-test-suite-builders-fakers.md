# PRD 0003 — Three-layer test suite (unit / integration / functional) with builders & fakers

**Status:** ready-for-agent · **Type:** AFK · **Delivery:** two phases, vertical slices (see
*Implementation Decisions → Phasing & slices*) · **Realizes:** a new test-strategy ADR (proposed
ADR-0006)

> Domain language per [`CONTEXT.md`](../../CONTEXT.md): **User** (tenant boundary — no User, Admin
> included, ever reads another User's journal), **Session**, **Raw**/**Cleaned**, **Revision**,
> **Cleanup**, **Synopsis**, **Summary**, **Correction**, **Topic**/**Person**/**Mood**,
> **Suggestion**, **provenance** (UserSet vs AiSuggested), **journaling-day**. Pattern reference:
> the PeakLimsApi test suite at `~/repos/PeakLimsApi/PeakLims/tests` (UnitTests / IntegrationTests /
> FunctionalTests / SharedTestHelpers). Auth architecture per
> [ADR-0002](../adr/0002-cookie-wrapped-jwt-auth.md); revisions per
> [ADR-0003](../adr/0003-append-only-content-revisions.md).

## Problem Statement

As a contributor to JournalRecall, the current tests don't give me leverage:

- **One mixed project does everything.** `JournalRecall.Api.Tests` (41 files) lumps pure-domain
  unit tests, in-process feature tests, and full-HTTP tests together. There's no layer to reach for,
  so almost everything has been written as a heavyweight `WebApplicationFactory` test even when no
  HTTP is involved.
- **No fast "bang-for-buck" middle layer.** There's nowhere to exercise a MediatR feature slice
  against the real DI scope and real SQLite without paying for a full HTTP round-trip, real auth, and
  a fresh host per test class.
- **Every full-HTTP test re-boots its own throwaway SQLite file and re-runs migrations**, so the
  suite doesn't scale to the hundreds of fast tests the feature surface now warrants.
- **No reusable test-data factories.** Aggregates are hand-built inline (`Session.Create(...)`) and
  DTOs are constructed by hand, so arrange steps are verbose, duplicated, and drift from the domain.
- **AI fakes are ad-hoc and trapped in the test project** (`ScriptableChatClient` lives inside
  `CleanupTestHost.cs`), so they can't be shared and the keyed `IChatClient` swap is re-invented per
  factory.

## Solution

As a contributor, I get a deliberate **three-layer** test architecture modelled on the PeakLims
pattern but adapted to JournalRecall's reality (SQLite not Postgres, Shouldly not FluentAssertions,
real first-party auth), plus a reusable **builder + faker** toolkit:

- **Unit** — pure domain and isolated logic; no host, no DB.
- **Integration** — the best bang for buck: real DI scope, real SQLite, the real MediatR slice,
  driven in-process via `SendAsync` with no HTTP. A **User is the isolation boundary**.
- **Functional** — anything that needs the web host: real auth flow, CSRF, the access gate, status
  codes, JSON shapes, and SSE streaming.
- **Builders & fakers** — `Fake{Aggregate}Builder` for domain aggregates (Bogus-backed defaults,
  fluent `With*` and compound-state helpers), `AutoFaker<T>` (Soenneker) for DTOs/requests, plus a
  shared, script-controllable AI chat client.

Delivered in **two phases**: Phase 1 stands up the four projects and proves every pathway on a single
pilot aggregate (**Session**); Phase 2 migrates the existing `Api.Tests` into the right layers and
retires it.

## User Stories

### Layering & project topology
1. As a contributor, I want four dedicated test projects (`UnitTests`, `IntegrationTests`,
   `FunctionalTests`, `SharedTestHelpers`), so that I reach for the right level of test by default.
2. As a contributor, I want `JournalRecall.AI.Tests` left untouched, so that the already-well-organized
   agent-runner suite isn't churned for no benefit.
3. As a contributor, I want a documented decision tree (`tests/README.md`) for "unit vs integration
   vs functional," so that I and any AFK agent place new tests consistently.
4. As a contributor, I want test names in `lowercase_with_underscores`, so that the new suite reads
   consistently with the PeakLims pattern we're adopting.

### Unit layer
5. As a contributor, I want to test domain aggregates (Session cleanup state machine, Mood, Location,
   Summary periods, Corrections) with no host or DB, so that domain logic is verified in milliseconds.
6. As a contributor, I want domain unit tests to construct aggregates via builders, so that arrange
   steps are short and intent-revealing.

### Integration layer (the bang-for-buck middle)
7. As a contributor, I want to send a MediatR command/query against the real DI scope and real
   SQLite without HTTP, so that I test a feature slice end-to-end cheaply.
8. As a contributor, I want each test to act as a fresh **User**, so that user-scoped data and
   list/count queries are isolated on a shared database with no reset step.
9. As a contributor, I want a `TestingServiceScope` exposing `GetService<T>`, `SendAsync`,
   `InsertAsync`, `FindAsync`, `ExecuteDbContextAsync`, and `SetUser`/`AsAdmin`, so that arrange and
   assert against the database are one-liners.
10. As a contributor, I want the current **User** set by placing a `ClaimsPrincipal` on a mocked
    `IHttpContextAccessor`, so that `ICurrentUserService` resolves identity exactly as in production.
11. As a contributor, I want HeimGuard permission checks stubbed permitted-by-default with a
    `SetUserNotPermitted(...)` escape hatch, so that authorization-failure paths are testable.
12. As a contributor, I want the **Cleanup** and **Summary** AI flows tested at the integration layer
    with a scripted chat client, so that AI behavior is verified deterministically without a model.
13. As a contributor, I want migrations to run once for the whole integration assembly, so that the
    suite stays fast as it grows.
14. As a contributor, I want a `ConfigureServices` hook in the integration fixture (as in PeakLims),
    so that I can swap `IHttpContextAccessor`, `IHeimGuardClient`, and the keyed `IChatClient`s.

### Functional layer (full web host)
15. As a contributor, I want functional tests to drive the **real** register→login flow and carry the
    real cookie/bearer + `X-CSRF` header, so that the genuine auth pipeline is exercised.
16. As a contributor, I want an opt-in fake-auth factory for tests that need an authenticated caller
    but aren't testing auth, so that I avoid login ceremony without leaving the HTTP layer.
17. As a contributor, I want fake auth to still pass through CSRF and the access gate (it only skips
    token issuance), so that the bypass never routes around middleware that production runs.
18. As a contributor, I want auth-behavior tests (login, refresh, CSRF rejection, gate redirects,
    registration policy, forced password change) to always use real auth, so that they prove the
    real thing.
19. As a contributor, I want to assert on the **SSE** `cleanup/stream` endpoint, so that streamed
    Cleanup progress is covered end-to-end.
20. As a contributor, I want `ApiRoutes` constants and `HttpClient` JSON helpers, so that functional
    tests are terse and route changes are caught in one place.

### Builders & fakers
21. As a contributor, I want a `FakeSessionBuilder` with fluent `With*` methods and compound-state
    helpers (`.Cleaned()`, `.Stale()`), so that I can arrange a Session in any lifecycle state in one
    line.
22. As a contributor, I want builders to call the real domain factory and mutators, so that test data
    can only reach states the domain actually permits.
23. As a contributor, I want free-text/random fields defaulted from Bogus and identity set explicitly
    via `WithUserId(...)`, so that a Session ties to its test's **User**.
24. As a contributor, I want DTOs and request records generated via `AutoFaker<T>` (Soenneker) with
    `RuleFor` overrides for constrained fields, so that request payloads are realistic without a
    bespoke builder each time.
25. As a contributor, I want a shared, script-controllable `ScriptableChatClient` (non-streaming and
    streamed), so that any layer can drive Cleanup/Summary deterministically.
26. As a contributor, I want a `FakeUserBuilder` and a `ClaimsPrincipal` helper, so that seeding a
    **User** and impersonating one are trivial.

### Migration (Phase 2)
27. As a maintainer, I want the 41 existing `Api.Tests` files redistributed into the right layer and
    `Api.Tests` deleted, so that there's a single, coherent structure.
28. As a maintainer, I want full-HTTP tests that don't actually need HTTP rewritten as integration
    tests, so that the suite gets faster and the layering rule is honored.
29. As a maintainer, I want the suite green at every step of the migration, so that we never trade
    coverage for structure.

## Implementation Decisions

### Architecture & ADRs
- **Mirror the PeakLims philosophy, swap the substrate.** Four-project split and the builder/faker +
  service-scope pattern come from PeakLims; the database engine, isolation mechanism, assertion
  library, and auth handling are adapted to JournalRecall.
- **A new ADR (proposed ADR-0006)** records the three-layer strategy and the "User-as-isolation-
  boundary on shared SQLite" decision, since it diverges from the PeakLims Testcontainers approach.
- **`JournalRecall.AI.Tests` is out of scope and untouched** — it tests a different assembly and has
  its own fakes (`FakeChatClient`, `InMemoryMcp`, `RoslynHarness`).

### Stack choices (divergences from PeakLims, deliberate)
- **Database:** SQLite (per project memory), **not** Testcontainers Postgres. **No Respawn**
  (poor SQLite support); isolation comes from the User boundary instead.
- **Assertions:** **Shouldly** (already standard here), **not** FluentAssertions (v8 is now a paid
  license). Lifted PeakLims tests are translated `.Should().Be(x)` → `.ShouldBe(x)`.
- **Auto-faking:** **`Soenneker.Utils.AutoBogus`** (maintained, .NET 10-ready) over classic AutoBogus,
  on plain **Bogus**.
- **Mocking:** **NSubstitute** (already present).

### Integration-test isolation model (the keystone)
- **One shared SQLite file** for the whole integration assembly, created by a collection fixture;
  **real migrations run once** at fixture init (keep the "migrations boot end-to-end" coverage — do
  **not** use `EnsureCreated`).
- **No reset between tests.** Each `TestingServiceScope` mints a **fresh random User** (direct
  `DbContext` insert, bypassing `UserManager`/Identity — integration tests never authenticate). All
  user-scoped data and list/count queries are naturally isolated even on the shared DB.
- **Integration tests bypass HTTP** — resolve a DI scope and `SendAsync` MediatR directly, so CSRF /
  access-gate / auth middleware never runs. Current **User** comes from a `ClaimsPrincipal` (with the
  `sub` claim = the seeded User's id) placed on a mocked `IHttpContextAccessor`.
- **Truly app-global tests** (registration policy, app settings — not User-scoped) do **not** belong
  in the shared integration layer; they go to functional, or to a dedicated serial `GlobalState`
  collection.

### Provider construction & current-user wiring
- The integration fixture obtains its provider from a **`WebApplicationFactory<Program>` subclass**
  (faithful to `Program`; reuses the existing `SkeletonWebApplicationFactory` groundwork) configured
  with the shared-DB connection string and a **`ConfigureServices` hook** (PeakLims-style) that swaps
  the mocked `IHttpContextAccessor`, mocked `IHeimGuardClient`, and the keyed AI `IChatClient`s.
- `SetUser` sets the mocked accessor's `HttpContext.User`; `SetUserIsPermitted` / `SetUserNotPermitted`
  configure the HeimGuard mock.

### Functional-test auth
- **Default = real auth.** `CreateAuthenticatedClientAsync()` runs the genuine register→login flow and
  returns an `HttpClient` carrying the real cookie/bearer + `X-CSRF` header.
- **Opt-in = fake auth.** A test-only fake authentication scheme, registered **only** in a dedicated
  `FakeAuthWebApplicationFactory` (never in `Program`), exposed via `client.AsUser(...)`/`AsAdmin()`.
  It skips only **token issuance**; the request still flows through CSRF and the access gate.
- **Guardrail (documented):** auth-behavior tests use real auth; fake auth is only "I need to be
  someone to reach this endpoint." If a test needs neither HTTP nor real auth, it should be an
  integration test.

### Deep modules (favored for isolated testing)
- **`TestingServiceScope`** — surface: `GetService<T>`, `SendAsync`, `InsertAsync`, `FindAsync`,
  `ExecuteDbContextAsync`, `SetUser`/`AsAdmin`/`SetUserNotPermitted`, `CurrentUserId`. Hides DI scope,
  fresh-User seeding, claims wiring, HeimGuard stubbing.
- **Integration `TestFixture`** (collection fixture) — surface: a static scope factory + the mocked
  seams. Hides one-time host boot, shared SQLite, migrations, the `ConfigureServices` swaps.
- **`TestingWebApplicationFactory` / `FakeAuthWebApplicationFactory`** — surface:
  `CreateAuthenticatedClientAsync()`, `AsUser()`. Hides real-login choreography vs. fake-scheme wiring.
- **`Fake{Aggregate}Builder`** (pilot: `FakeSessionBuilder`) — surface: fluent `With*` +
  `.Cleaned()`/`.Stale()`. Hides the real `Create` + mutator choreography and Bogus defaults.
- **`ScriptableChatClient`** (moved to SharedTestHelpers) — surface: `Script(...)`. Hides both the
  non-streaming and streamed (`GetStreamingResponseAsync`) `IChatClient` implementations.
- **SSE reader helper** — surface: `ReadServerSentEventsAsync()`. Hides `text/event-stream` parsing.

### Conventions
- **Naming:** `lowercase_with_underscores`.
- **Folders:** `UnitTests/Domain/{Aggregate}/`, `IntegrationTests/FeatureTests/{Aggregate}/`,
  `FunctionalTests/{Area}/`, `SharedTestHelpers/Fakes/{Aggregate}/` + `SharedTestHelpers/Utilities/`.
- **Parallelization:** Unit = parallel; Integration = single serial collection (shared SQLite file);
  Functional = single serial collection; cross-assembly parallelism stays on. A `GlobalState`
  collection (parallelization disabled) is the escape hatch for app-global tests.
- **Global usings** per project (`Xunit`, `Shouldly`, helper + SharedTestHelpers namespaces).
- **`TestingConsts`** (seed ids, default timezone) lives in SharedTestHelpers.

### Phasing & slices (final cut handled by `to-issues`)
- **Phase 1 — Scaffold + pilot (Session).** Stand up the four projects (wired into `JournalRecall.slnx`,
  packages via `Directory.Packages.props`); build the deep modules above; move `ScriptableChatClient`
  into SharedTestHelpers; write **one meaningful reference test per pathway**:
  - *Unit:* a Session domain test (cleanup state machine) rewritten on `FakeSessionBuilder`.
  - *Integration:* `CreateSession` persists + scopes to current User; `GetSession` cross-User denial;
    one **Cleanup** test driving the scripted AI client through the MediatR handler.
  - *Functional:* create-session over real auth; a fake-auth GET; the **SSE `cleanup/stream`** test.
  - Plus `tests/README.md` (the layer decision tree, User-isolation rule, auth rule).
- **Phase 2 — Migrate & retire.** Redistribute the 41 `Api.Tests` files into the right layers,
  rewriting HTTP-but-not-really tests as integration tests, lowercasing names, and adding builders
  per aggregate as needed; delete `Api.Tests`. Suite stays green throughout.

## Testing Decisions

This PRD's deliverable *is* test infrastructure, so the "tests" here are the reference tests that
prove each pathway plus the migrated suite.

- **What makes a good test:** assert **external behavior**, not implementation. Unit tests assert
  domain outcomes via public aggregate API; integration tests assert persisted state and returned
  DTOs after a `SendAsync`, scoped to the test's **User**; functional tests assert HTTP status,
  response shape, headers/cookies, and streamed SSE events. Builders are used to arrange, never
  asserted on.
- **Modules covered by reference tests (Phase 1):** `FakeSessionBuilder` (via the unit + integration
  tests that consume it), `TestingServiceScope` + integration `TestFixture` (via the CreateSession /
  GetSession / Cleanup tests), `ScriptableChatClient` streaming + non-streaming (via the Cleanup
  integration test and the SSE functional test), `TestingWebApplicationFactory` and
  `FakeAuthWebApplicationFactory` (via the real-auth and fake-auth functional tests), the SSE reader
  helper (via the `cleanup/stream` test).
- **Prior art in this repo:** `SkeletonWebApplicationFactory` / `CleanupWebApplicationFactory`
  (host + keyed-client-swap pattern to generalize), the existing `Domain/*Tests` (the unit shape),
  and `AI.Tests/Fakes/*` (deterministic-fake and harness patterns).
- **Pattern reference (external):** PeakLims `TestingServiceScope`, `TestFixture`, `TestBase`,
  `Fake*Builder`, `ApiRoutes`, `HttpClientExtensions`.

## Out of Scope

- **`JournalRecall.AI.Tests`** — left entirely as-is.
- **Frontend / SPA tests** (Vite client) — not addressed here.
- **Mass coverage expansion** — Phase 2 *migrates and re-layers* existing tests (and adds builders as
  needed); writing net-new coverage for previously-untested behavior is a follow-up.
- **Testcontainers / Postgres / Respawn** — explicitly rejected in favor of SQLite + User isolation.
- **A fake-auth bypass at the integration layer** — unnecessary; integration sets a `ClaimsPrincipal`
  directly.
- **Load/performance and security testing.**

## Further Notes

- The integration fixture reusing `WebApplicationFactory<Program>` (vs. a hand-rolled standalone host)
  is the recommended resolution for prod-parity; revisit only if host-boot cost in the integration
  assembly proves material.
- Session is the deliberate pilot precisely because it is the richest aggregate (revisions, cleanup,
  AI, metadata, SSE) — proving the skeleton on it flushes out every mechanism before the Phase 2 bulk
  move.
- Consider whether the shared `ScriptableChatClient` and builder toolkit should later be referenced by
  `AI.Tests` to reduce duplication — allowed, but not forced by this PRD.
