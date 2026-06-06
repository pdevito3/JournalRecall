# TEST-0014 — Retire `Api.Tests`

**Phase:** 10 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0003

## What to build

The capstone: with every test redistributed, delete the old mixed project and leave a single, coherent
three-layer structure. Suite stays green.

- Confirm `JournalRecall.Api.Tests` holds no remaining tests (only leftover infra:
  `SkeletonWebApplicationFactory`, `AssemblyInfo`, any test hosts) — fold anything still useful into the
  new harnesses, then **delete the project** and remove it from `JournalRecall.slnx`.
- Ensure the keyed-client-swap / host-boot patterns previously in `SkeletonWebApplicationFactory` are
  fully covered by `TestingWebApplicationFactory` / `FakeAuthWebApplicationFactory` / the integration
  `TestFixture` before deleting.
- `JournalRecall.AI.Tests` remains untouched.

## Acceptance criteria

- [ ] `JournalRecall.Api.Tests` is deleted and removed from `JournalRecall.slnx`; nothing references it.
- [ ] No test coverage is lost — every behavior formerly in `Api.Tests` now lives in the unit,
      integration, or functional project.
- [ ] `JournalRecall.AI.Tests` is unchanged.
- [ ] The full solution builds and the entire test suite is green.

## Blocked by

- #TEST-0006
- #TEST-0007
- #TEST-0008
- #TEST-0009
- #TEST-0010
- #TEST-0011
- #TEST-0012
- #TEST-0013
