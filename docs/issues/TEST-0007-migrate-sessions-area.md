# TEST-0007 — Migrate Sessions area (sessions, revisions, timeline, journaling-day)

**Phase:** 10 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0003

## What to build

Redistribute the core Session feature tests into the right layer. Tests that don't actually exercise
HTTP become **integration** tests (`SendAsync`, fresh-User isolation); those that need status
codes/JSON/headers stay **functional**. Suite stays green.

- Migrate `SessionTests`, `SessionTimelineTests`, `RawRevisionTests`, `JournalingDayTests` from
  `Api.Tests` into `IntegrationTests/FeatureTests/Session/` and/or `FunctionalTests/Sessions/`.
- Rewrite HTTP-but-not-really tests as integration tests scoped to the test's User; keep genuine
  end-to-end HTTP behavior (timeline query shapes, status codes) functional.
- Lowercase names; Shouldly assertions; arrange via `FakeSessionBuilder` / `FakeCreateSessionRequest`.

## Acceptance criteria

- [ ] Session, timeline, raw-revision, and journaling-day tests live in the integration and/or functional
      layer per the decision tree, named `lowercase_with_underscores`, using Shouldly and the builders.
- [ ] Tests that need no HTTP are integration tests scoped to a fresh User; HTTP-shape tests stay functional.
- [ ] The migrated files are removed from `Api.Tests`; the full suite is green.

## Blocked by

- #TEST-0003
- #TEST-0004
