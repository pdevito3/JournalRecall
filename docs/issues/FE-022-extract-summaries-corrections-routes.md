# FE-022 — Extract the Summaries + Corrections routes into feature components

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

Extract the heavier logic from the **Summaries** (~200-line) and **Corrections** (~140-line) routes
into testable feature components, leaving the routes as thin `createFileRoute` + render shells —
applying the thin-shell pattern consistently across the remaining data routes.

## Acceptance criteria

- [ ] Summaries and Corrections route logic lives in feature components; both routes are thin shells.
- [ ] No behavior change; a dev-browser pass confirms each screen still works.
- [ ] App boots and existing tests stay green.

## Blocked by

- None - can start immediately
