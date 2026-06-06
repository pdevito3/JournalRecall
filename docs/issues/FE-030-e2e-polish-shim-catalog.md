# FE-030 — (NICE) `waitFor` shim, reference-flow catalog, failure capture

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

E2E polish, built on the FE-028 helper module:

- A `waitFor` polling shim if the dev-browser sandbox lacks Playwright's `expect` (per the FE-028
  spike).
- A small catalog of named reference flows (the common journeys) that compose the helpers.
- Screenshot-plus-`role=alert`-text capture on failure, for quick diagnosis.

## Acceptance criteria

- [ ] If the sandbox lacks `expect`, a `waitFor` polling shim is provided and used by the helpers.
- [ ] A named reference-flow catalog exists, composing the FE-028 helpers.
- [ ] On failure, a flow captures a screenshot plus the `role=alert` text.

## Blocked by

- FE-028
