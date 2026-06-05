# 0032 — Migrate Metadata & Location

**Phase:** 10 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0003

## What to build

Redistribute the metadata and location feature tests into the right layer, following the decision tree.
Suite stays green.

- Migrate `MetadataTests` (Topics, People, Mood + filtering) and `LocationTests` (opt-in) from
  `Api.Tests` into `IntegrationTests/FeatureTests/{Aggregate}/` and/or `FunctionalTests/{Area}/`.
- Rewrite HTTP-but-not-really tests as integration tests scoped to the test's User; keep status-code /
  JSON-shape coverage functional.
- Lowercase names; Shouldly assertions; arrange via builders (add thin metadata/location builders only if
  arrange is verbose).

## Acceptance criteria

- [ ] Metadata and Location tests live in the integration and/or functional layer per the decision tree,
      named `lowercase_with_underscores`, using Shouldly.
- [ ] Metadata filtering and location opt-in behavior remain covered; non-HTTP cases are integration tests.
- [ ] The migrated files are removed from `Api.Tests`; the full suite is green.

## Blocked by

- #0027
- #0028
