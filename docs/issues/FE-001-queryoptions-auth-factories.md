# FE-001 — Auth `queryOptions()` factories + collapse root `beforeLoad` duplication

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

The keystone. Introduce `queryOptions()` factories for the auth feature's two server-state queries —
the current session (`me`) and the public auth-config — as the single source of truth for each
query's key, `queryFn`, and `staleTime`. The auth hooks (`useMe`, `useAuthConfig`) call
`useQuery(factory())`; the root route's `beforeLoad` calls `ensureQueryData(factory())`.

Delete the hand-duplicated `me`/auth-config query definitions that currently live inline in the root
`beforeLoad` so the access gate and the components can never disagree on a cache key or `staleTime`.

This factory is a deep module with a simple interface (args → options) that rarely changes; it
unblocks the loader work (FE-002+) and the selector work (FE-003).

## Acceptance criteria

- [ ] One `queryOptions()` factory exists for `me` and one for auth-config; each defines key, `queryFn`,
      and `staleTime` exactly once.
- [ ] `useMe`/`useAuthConfig` and the root `beforeLoad` both consume the factories; the inline query
      definitions in `beforeLoad` are removed.
- [ ] No behavior change to the access gate — existing auth/setup/login redirects still hold; app boots
      and existing tests stay green.

## Blocked by

- None - can start immediately
