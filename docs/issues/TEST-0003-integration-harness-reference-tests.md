# TEST-0003 — Integration harness + reference tests

**Phase:** 10 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0003

## What to build

The bang-for-buck middle layer: send a MediatR command/query against the real DI scope and real SQLite
with **no HTTP**, isolated by a fresh **User** per test on a shared database. Proven by reference tests
on the Session pilot.

- **Collection `TestFixture`** — one shared SQLite file for the whole integration assembly; real
  migrations run **once** at fixture init (not `EnsureCreated` — keep the "migrations boot end-to-end"
  coverage). Provider comes from a `WebApplicationFactory<Program>` subclass configured with the shared
  connection string and a PeakLims-style **`ConfigureServices` hook** that swaps a mocked
  `IHttpContextAccessor`, a mocked `IHeimGuardClient`, and the keyed AI `IChatClient`s.
- **`TestingServiceScope`** — surface: `GetService<T>`, `SendAsync`, `InsertAsync`, `FindAsync`,
  `ExecuteDbContextAsync`, `SetUser` / `AsAdmin` / `SetUserNotPermitted`, `CurrentUserId`. Each scope
  mints a **fresh random User** via a direct `DbContext` insert (bypassing `UserManager`/Identity) and
  wires a `ClaimsPrincipal` onto the mocked accessor so `ICurrentUserService` resolves identity exactly
  as in production. HeimGuard is permitted-by-default with a `SetUserNotPermitted(...)` escape hatch.
- **`TestBase`** for the serial integration collection.
- **Reference tests** (`lowercase_with_underscores`): CreateSession persists and scopes to the current
  User; GetSession denies cross-User access; one **Cleanup** test driving the scripted AI client through
  the real MediatR handler. Assert persisted state and returned DTOs, never implementation.

## Acceptance criteria

- [ ] Migrations run exactly once for the assembly against a single shared SQLite file; no per-test reset.
- [ ] `TestingServiceScope` exposes the listed surface; each scope acts as a fresh isolated User, with
      `SetUser`/`AsAdmin`/`SetUserNotPermitted` wired through the mocked accessor and HeimGuard mock.
- [ ] CreateSession persists + scopes to the current User; a cross-User GetSession is denied.
- [ ] A Cleanup integration test drives `ScriptableChatClient` through the MediatR handler deterministically.
- [ ] Integration tests run in a single serial collection and bypass HTTP (no CSRF / gate / auth middleware).

## Blocked by

- #TEST-0002
