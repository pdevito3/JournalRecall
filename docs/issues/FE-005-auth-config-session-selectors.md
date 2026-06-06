# FE-005 — (NICE) auth-config slice + single-item session selectors

**Phase:** 12 · **Type:** AFK · **Status:** closed — not needed · **Realizes:** PRD-0005

> **Closed as not-needed (2026-06-06).** No consumer materialized across FE-006..FE-030: auth-config
> (`needsSetup`/`selfRegistrationEnabled`) is read directly through the auth hooks, and a single
> session is read via its own `sessionQueryOptions(id)` query — not by selecting one item out of the
> list cache. Building either selector now would be speculative, so per the spec this closes without
> them.

## What to build

Optional follow-on selectors, implemented **only when a concrete consumer needs them**:

- An auth-config `select` slice for `selfRegistrationEnabled` / `needsSetup`.
- A single-item selector over the session list (read one session out of the list cache).

If no consumer materializes during the rest of the phase, close this as "not needed" rather than
adding speculative selectors.

## Acceptance criteria

- [ ] If built: each selector is module-level, factory-backed, and has a real call site.
- [ ] If built: a unit test covers the slice against representative payloads.
- [x] If no consumer exists, the issue is closed as not-needed with a one-line rationale.

## Blocked by

- FE-002
- FE-004
