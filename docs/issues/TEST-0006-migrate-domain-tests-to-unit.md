# TEST-0006 — Migrate domain tests → UnitTests

**Phase:** 10 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0003

## What to build

Move the pure-domain tests out of `Api.Tests` into `UnitTests`, the cheapest layer, rewriting arrange
steps on builders where it helps. First Phase 2 slice; proves the unit layer at scale. Suite stays green.

- Relocate the `Api.Tests/Domain/*` tests — Session aggregate, Correction, CorrectionApplier, Mood,
  Location value object, Summary aggregate, Summary periods, RefreshToken service — into
  `UnitTests/Domain/{Aggregate}/`, plus `CurrentUserService` / `AuditFields` tests if they need no host
  or DB.
- Lowercase test names to `lowercase_with_underscores`; translate any `.Should().Be(x)` to `.ShouldBe(x)`.
- Use `FakeSessionBuilder` (and add thin builders for other aggregates only where arrange is verbose);
  assert domain outcomes via the public aggregate API.

## Acceptance criteria

- [ ] All pure-domain tests live under `UnitTests/Domain/` and pass with no host or DB.
- [ ] Names are `lowercase_with_underscores`; assertions use Shouldly.
- [ ] Session arrange uses `FakeSessionBuilder`; other aggregates use builders only where it reduces noise.
- [ ] The migrated tests are removed from `Api.Tests`; the full suite is green.

## Blocked by

- #TEST-0002
