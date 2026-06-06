# JournalRecall test suite

A deliberate **three-layer** architecture (PRD-0003, [ADR-0006](../docs/adr/0006-three-layer-test-suite-user-isolated-sqlite.md)),
modelled on the PeakLims pattern but adapted to JournalRecall's substrate (SQLite, Shouldly, first-party
cookie+JWT auth). Reach for the cheapest layer that can actually prove the behavior.

| Project | What it tests | Cost | Parallelism |
| --- | --- | --- | --- |
| `JournalRecall.UnitTests` | Pure domain & isolated logic — no host, no DB | milliseconds | parallel |
| `JournalRecall.IntegrationTests` | A real MediatR slice over real SQLite via `SendAsync` — **no HTTP** | fast | one serial collection |
| `JournalRecall.FunctionalTests` | The full web host — auth, CSRF, the access gate, status codes, JSON, SSE | slow | one serial collection |
| `JournalRecall.SharedTestHelpers` | Builders, fakers, the scriptable AI clients (a library, not tests) | — | — |

`JournalRecall.AI.Tests` is a separate, untouched suite for the agent-runner assembly.

## Decision tree — where does my test go?

```
Does the behavior need the web host?
  (HTTP status codes, JSON shape, Set-Cookie/headers, CSRF, the access gate, SSE, the real auth pipeline)
│
├─ YES ─────────────────────────────────────────────► FunctionalTests
│
└─ NO. Does it need the database / a real DI scope / a MediatR slice?
   │
   ├─ YES ──────────────────────────────────────────► IntegrationTests
   │
   └─ NO (pure domain logic on aggregates/value objects) ► UnitTests
```

- **Unit** — construct the aggregate (via a `Fake{Aggregate}Builder`) and assert domain outcomes through
  its public API. No `WebApplicationFactory`, no `DbContext`. Example:
  `UnitTests/Domain/Sessions/session_cleanup_state_machine_tests.cs`.
- **Integration** — resolve a `TestingServiceScope`, `SendAsync` a command/query (or drive a scoped
  service like `SessionCleanupRunner`), and assert persisted state + returned DTOs. This is the
  bang-for-buck middle: no HTTP round-trip, no auth ceremony. Example:
  `IntegrationTests/FeatureTests/Sessions/`.
- **Functional** — boot a real client and assert the HTTP contract. Example:
  `FunctionalTests/Sessions/`.

> Rule of thumb: if a test spins up a full HTTP client only to reach a handler whose behavior is really
> about persisted state, it's an **integration** test wearing an HTTP costume — move it down a layer.

## The User-isolation rule (integration)

Integration tests share **one SQLite file** for the whole assembly, with **no reset between tests**.
Isolation comes from the **User boundary**: every `TestingServiceScope` mints a fresh random User, seeds
the row directly, and sets a matching `ClaimsPrincipal` on the mocked `IHttpContextAccessor`, so the
per-User tenant query filter scopes all data and list/count queries automatically.

- Act as a fresh User per test; for cross-User checks, use **two** scopes (each is a distinct User).
- **Truly app-global** tests (registration policy, app-wide settings — not User-scoped) do **not** belong
  in the shared integration layer. Put them in functional, or in a dedicated serial `GlobalState`
  collection, so they never collide with the shared, no-reset database.
- Migrations run **once** for the assembly (the app's real startup migrations, not `EnsureCreated`).

## The auth rule (functional)

- **Default = real auth.** `TestingWebApplicationFactory.CreateAuthenticatedClientAsync()` runs the genuine
  register→login flow; the client carries the real cookie + `X-CSRF` header.
- **Opt-in = fake auth.** `FakeAuthWebApplicationFactory` registers a test-only scheme (never in `Program`),
  driven by `client.AsUser(...)`/`AsAdmin()`. It skips **only** token issuance — the request still flows
  through CSRF and the access gate.
- **Auth-behavior tests always use real auth** (login, refresh rotation, CSRF rejection, cookie hardening,
  gate redirects, registration policy, forced password change). Fake auth is only "I need to *be someone*
  to reach this endpoint." If a test needs neither HTTP nor real auth, it's an integration test.

## Conventions

- **Test names:** `lowercase_with_underscores` (class and method).
- **Assertions:** Shouldly (`.ShouldBe(x)`), never FluentAssertions.
- **Arrange:** builders, never asserted on. Generate DTOs/requests with `AutoFaker<T>` (Soenneker).
- **Folders:** `UnitTests/Domain/{Aggregate}/`, `IntegrationTests/FeatureTests/{Aggregate}/`,
  `FunctionalTests/{Area}/`, `SharedTestHelpers/Fakes/{Area}/` + `SharedTestHelpers/Utilities/`.

## The deep modules

- **`FakeSessionBuilder`** (`SharedTestHelpers/Fakes/Sessions`) — fluent `With*` + `.Cleaned()`/`.Stale()`/
  `.Failed()`/`.WithHandEdit()`; calls the real factory + mutators so it can only reach legal states.
- **`FakeUserBuilder`** / **`FakeClaimsPrincipal`** (`SharedTestHelpers/Fakes/Identity`) — seed a User and
  impersonate one (`sub` + role claims, exactly as production shapes them).
- **`FakeCreateSessionRequest`** (`SharedTestHelpers/Fakes/Sessions`) — AutoFaker request generation.
- **`ScriptableChatClient`** / **`ScriptableSummaryChatClient`** (`SharedTestHelpers/Fakes/Ai`) — drive
  Cleanup/Summary deterministically (streamed and non-streamed).
- **`TestingServiceScope`** + integration **`TestFixture`** — the fresh-User DI scope and the one-time
  shared-SQLite host.
- **`TestingWebApplicationFactory`** / **`FakeAuthWebApplicationFactory`** — real-auth vs fake-auth hosts.
- **`ApiRoutes`**, **`HttpClientExtensions`** (JSON helpers, `AsUser`/`AsAdmin`, `ReadServerSentEventsAsync`).

See [ADR-0006](../docs/adr/0006-three-layer-test-suite-user-isolated-sqlite.md) for the rationale and
[PRD-0003](../docs/prd/0003-three-layer-test-suite-builders-fakers.md) for the full strategy.
