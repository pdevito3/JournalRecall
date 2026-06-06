# FE-005 — (NICE) auth-config slice + single-item session selectors

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

Optional follow-on selectors, implemented **only when a concrete consumer needs them**:

- An auth-config `select` slice for `selfRegistrationEnabled` / `needsSetup`.
- A single-item selector over the session list (read one session out of the list cache).

If no consumer materializes during the rest of the phase, close this as "not needed" rather than
adding speculative selectors.

## Acceptance criteria

- [ ] If built: each selector is module-level, factory-backed, and has a real call site.
- [ ] If built: a unit test covers the slice against representative payloads.
- [ ] If no consumer exists, the issue is closed as not-needed with a one-line rationale.

## Blocked by

- FE-002
- FE-004
