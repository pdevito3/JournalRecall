# FE-011 — Router-level default pending/error components

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

Configure router-level default pending and error components so every screen's loading and failure
states look and behave the same, and remove the repeated per-component `isLoading` / `isError`
branches on the loader-backed routes (Admin, Corrections, Session detail).

**Decision (record in the issue/PR):** whether loaded routes use `useSuspenseQuery` or keep
`useQuery` + branches under the router defaults. Server state stays owned by React Query either way
(loaders prime the cache; components never read `useLoaderData`).

## Acceptance criteria

- [ ] Router declares default pending and error components; loader-backed routes drop their bespoke
      `isLoading`/`isError` branches.
- [ ] The Suspense-vs-branches decision is recorded in the PR description with its rationale.
- [ ] Loading and error UI is visibly consistent across the converted routes.

## Blocked by

- FE-007
- FE-008
