# FE-012 — (NICE) `defaultPreloadStaleTime: 0` so Query owns preload lifetime

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

Set `defaultPreloadStaleTime: 0` on the router (under the existing `defaultPreload: 'intent'`) so that
React Query — not the router's preload cache — owns the cache lifetime of preloaded route data. With
loaders in place (FE-007/008), hover/intent preloading then warms route data, not just the auth
queries.

## Acceptance criteria

- [ ] `defaultPreloadStaleTime: 0` is set on the router config.
- [ ] Hovering a link to a loader-backed route warms its data; React Query's `staleTime` governs
      freshness thereafter.
- [ ] No double-fetch regression on navigation.

## Blocked by

- FE-007
- FE-008
