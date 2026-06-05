# 0025 — Test-suite scaffold: four projects, packages, conventions, ADR-0006

**Phase:** 10 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0003, ADR-0006

## What to build

Stand up the empty three-layer test architecture so every later slice has somewhere to land. This is
pure foundation — no reference tests yet — and everything in PRD-0003 blocks on it.

- **Four new projects** — `JournalRecall.UnitTests`, `JournalRecall.IntegrationTests`,
  `JournalRecall.FunctionalTests`, `JournalRecall.SharedTestHelpers` — wired into `JournalRecall.slnx`.
  `JournalRecall.AI.Tests` and `JournalRecall.Api.Tests` are left untouched for now.
- **Packages** centralized in `Directory.Packages.props`: xUnit, Shouldly, NSubstitute, Bogus,
  `Soenneker.Utils.AutoBogus`, and `Microsoft.AspNetCore.Mvc.Testing`. No FluentAssertions, no
  Testcontainers/Respawn (explicitly rejected — SQLite + User-isolation instead).
- **Conventions**: per-project global usings (`Xunit`, `Shouldly`, the helper namespaces);
  `lowercase_with_underscores` test naming; the folder layout from the PRD
  (`UnitTests/Domain/{Aggregate}`, `IntegrationTests/FeatureTests/{Aggregate}`,
  `FunctionalTests/{Area}`, `SharedTestHelpers/Fakes/{Aggregate}` + `/Utilities`).
- **`TestingConsts`** in SharedTestHelpers (seed ids, default timezone).
- **ADR-0006** (`docs/adr/0006-*.md`) recording the three-layer strategy and the
  "User-as-isolation-boundary on shared SQLite" decision (divergence from the PeakLims Testcontainers
  approach). All decisions are already settled in PRD-0003 — this is the durable record of them.

## Acceptance criteria

- [ ] The four projects exist, build, and are referenced in `JournalRecall.slnx`; the solution builds.
- [ ] `Directory.Packages.props` carries the new packages; no FluentAssertions / Testcontainers / Respawn.
- [ ] Each project has its global usings; `TestingConsts` lives in SharedTestHelpers.
- [ ] ADR-0006 is committed and records the three-layer + User-isolation decision, cross-linked from the
      test-strategy area.
- [ ] `JournalRecall.AI.Tests` and `JournalRecall.Api.Tests` are unchanged.

## Blocked by

- None - can start immediately
