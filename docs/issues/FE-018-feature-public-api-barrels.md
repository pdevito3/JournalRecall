# FE-018 — Per-feature public-API barrels

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

Add a public-API barrel (`index.ts`) to each feature exposing only its intended surface, and switch
route imports to consume features through the barrel rather than reaching into internal files. This
lets a feature's internals be refactored freely and gives the import-boundary rule (FE-019) a clean
public edge to enforce.

## Acceptance criteria

- [ ] Each feature exposes a public-API barrel; internal modules not meant for cross-boundary use are
      not re-exported.
- [ ] Route imports go through feature barrels, not deep internal paths.
- [ ] App boots and existing tests stay green.

## Blocked by

- None - can start immediately
