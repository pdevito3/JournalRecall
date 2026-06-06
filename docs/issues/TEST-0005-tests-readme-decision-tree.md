# TEST-0005 — `tests/README.md` decision tree

**Phase:** 10 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0003

## What to build

The written guide that lets any contributor or AFK agent place a new test at the right layer without
re-deriving the strategy. Closes Phase 1.

- **`tests/README.md`** with the **unit vs integration vs functional decision tree**: unit = pure domain,
  no host/DB; integration = real DI scope + real SQLite via `SendAsync`, no HTTP; functional = anything
  needing the web host (auth, CSRF, gate, status codes, JSON, SSE).
- The **User-isolation rule**: integration tests share one SQLite file with no reset; each test acts as a
  fresh User; truly app-global tests (registration policy, app settings) go to functional or a dedicated
  serial `GlobalState` collection — not the shared integration layer.
- The **auth rule**: functional defaults to real auth; fake auth is opt-in and only "I need to be someone
  to reach this endpoint" — auth-behavior tests always use real auth; if a test needs neither HTTP nor
  real auth, it's an integration test.
- Pointers to the deep modules (`FakeSessionBuilder`, `TestingServiceScope`, the factories,
  `ScriptableChatClient`, the SSE reader) and to ADR-0006.

## Acceptance criteria

- [ ] `tests/README.md` documents the three-layer decision tree with concrete "reach for X when…" guidance.
- [ ] The User-isolation rule and the `GlobalState` escape hatch are written down.
- [ ] The real-auth-by-default / fake-auth-opt-in rule is written down.
- [ ] The doc links the builder/faker toolkit, the harnesses, and ADR-0006.

## Blocked by

- #TEST-0003
- #TEST-0004
