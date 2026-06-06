# TEST-0010 — Migrate Summaries

**Phase:** 10 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0003

## What to build

Redistribute the Summary feature tests, driving the Summary AI flow through the shared
`ScriptableChatClient`. Suite stays green.

- Migrate `SummaryTests` (day/week on-demand) and `SummaryRollupTests` (month/quarter/year roll-ups +
  staleness propagation) from `Api.Tests` into `IntegrationTests/FeatureTests/Summary/` and/or
  `FunctionalTests/Summaries/`.
- **Absorb `SummaryTestHost`**: the keyed `IChatClient` swap comes from the integration `TestFixture` /
  functional factories — delete the bespoke host once its tests move.
- Rewrite HTTP-but-not-really tests as integration tests scoped to the test's User; keep HTTP-shape
  coverage functional. Add a `FakeSummaryBuilder` only if arrange is verbose.
- Lowercase names; Shouldly assertions.

## Acceptance criteria

- [ ] Summary and roll-up/staleness tests live in the integration and/or functional layer per the decision
      tree, named `lowercase_with_underscores`, using Shouldly.
- [ ] Summary AI flow is driven by the shared `ScriptableChatClient`; `SummaryTestHost` is deleted.
- [ ] The migrated files are removed from `Api.Tests`; the full suite is green.

## Blocked by

- #TEST-0003
- #TEST-0004
