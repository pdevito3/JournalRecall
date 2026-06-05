# 0033 — Migrate Cleanup & Corrections

**Phase:** 10 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0003

## What to build

Redistribute the AI Cleanup and Corrections feature tests, driving the AI flows through the shared
`ScriptableChatClient` at the integration layer wherever HTTP isn't the point. Suite stays green.

- Migrate `CleanupTests`, `CleanupSuggestionTests`, `CleanedEditTests`, and the feature-level
  `CorrectionTests` from `Api.Tests` into `IntegrationTests/FeatureTests/{Aggregate}/` and/or
  `FunctionalTests/{Area}/`.
- **Absorb `CleanupTestHost`**: the keyed `IChatClient` swap now comes from the integration `TestFixture`
  `ConfigureServices` hook / the functional factories — delete the bespoke host once its tests move.
- Cleanup/Summary AI behavior is driven deterministically via `ScriptableChatClient` (streamed and
  non-streamed); the SSE `cleanup/stream` path stays functional.
- Lowercase names; Shouldly assertions; arrange via builders.

## Acceptance criteria

- [ ] Cleanup, cleanup-suggestion, cleaned-edit, and correction feature tests live in the integration
      and/or functional layer per the decision tree, named `lowercase_with_underscores`, using Shouldly.
- [ ] AI flows are driven by the shared `ScriptableChatClient`; `CleanupTestHost` is deleted.
- [ ] The migrated files are removed from `Api.Tests`; the full suite is green.

## Blocked by

- #0027
- #0028
