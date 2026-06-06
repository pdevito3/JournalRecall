# TEST-0002 — SharedTestHelpers builders/fakers + unit-layer proof

**Phase:** 10 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0003

## What to build

The reusable builder + faker toolkit, proven end-to-end by the first real unit test. After this slice a
contributor can arrange a `Session` in any lifecycle state in one line and a domain test runs in
milliseconds with no host or DB.

- **`FakeSessionBuilder`** — fluent `With*` methods, compound-state helpers `.Cleaned()` / `.Stale()`,
  free-text/random fields defaulted from Bogus, identity set explicitly via `WithUserId(...)`. Builders
  call the **real** domain factory and mutators, so test data can only reach states the domain permits.
- **`FakeUserBuilder`** and a **`ClaimsPrincipal` helper** so seeding a User and impersonating one are
  trivial (the `sub` claim ties to the seeded User's id).
- **`FakeCreateSessionRequest`** via `AutoFaker<T>` (Soenneker) with `RuleFor` overrides for constrained
  fields, plus the global AutoFaker configuration.
- **Move `ScriptableChatClient`** out of `Api.Tests/CleanupTestHost.cs` into SharedTestHelpers, with
  **both** non-streaming and streamed (`GetStreamingResponseAsync`) implementations behind a `Script(...)`
  surface, so any layer can drive Cleanup/Summary deterministically.
- **Unit reference test**: the Session cleanup state-machine test rewritten on `FakeSessionBuilder`,
  asserting domain outcomes through the public aggregate API. This is the proof the builder toolkit works.

## Acceptance criteria

- [ ] `FakeSessionBuilder` arranges a Session in raw / cleaned / stale states in one line via the real
      domain factory and mutators; random fields come from Bogus; `WithUserId` sets identity.
- [ ] `FakeUserBuilder`, the `ClaimsPrincipal` helper, and `FakeCreateSessionRequest` (AutoFaker) exist
      and are usable from any test project.
- [ ] `ScriptableChatClient` lives in SharedTestHelpers, supports streaming and non-streaming, and is no
      longer defined inside `Api.Tests` (its old call sites still compile or are bridged).
- [ ] A passing unit test exercises the Session cleanup state machine using `FakeSessionBuilder`, named
      `lowercase_with_underscores`, with no host or DB.

## Blocked by

- #TEST-0001
