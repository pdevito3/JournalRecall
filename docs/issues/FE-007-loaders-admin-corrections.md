# FE-007 — `ensureQueryData` loaders on the Admin + Corrections routes

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

Add `ensureQueryData` loaders to the Admin and Corrections routes so they start fetching during
navigation rather than after mount, killing the mount→fetch waterfall. The loader primes the cache
via the FE-002 factories; components keep reading via `useQuery` (never `useLoaderData`) so
focus/reconnect refetch, dedup, and GC keep working.

## Acceptance criteria

- [ ] Admin and Corrections routes have loaders that `ensureQueryData` their primary queries via the
      factories.
- [ ] Components on those routes read server state via `useQuery`, not `useLoaderData`.
- [ ] Navigating to each route begins fetching during the transition (verified via a dev-browser pass
      or network observation); no behavior regression.

## Blocked by

- FE-002
