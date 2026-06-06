# FE-002 — `queryOptions()` factories for the remaining feature queries

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

Extend the keystone pattern from FE-001 to the rest of the features: sessions (and their revision
streams), corrections, summaries, admin, and settings. Each query gets one `queryOptions()` factory,
parameterized where the key is (e.g. by `sessionId`, by summary period/date). Hooks call
`useQuery(factory(args))`; downstream loaders (FE-007/008) and `select`-based hooks build on the same
factories so priming the cache in a loader and reading it in a component are guaranteed consistent.

No new queries and no key changes that would invalidate existing caches — this is a refactor of how
existing queries are declared.

## Acceptance criteria

- [ ] Every existing feature query (sessions, revisions, corrections, summaries, admin, settings) is
      defined through a `queryOptions()` factory, parameterized where its key is.
- [ ] Existing hooks are rewritten to call `useQuery(factory(args))`; query keys and `staleTime`s are
      unchanged from today.
- [ ] App boots, all screens still fetch correctly, existing tests stay green.

## Blocked by

- FE-001
