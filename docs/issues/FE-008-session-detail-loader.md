# FE-008 — Session-detail loader + non-awaited Revision-stream prefetch

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

Add an `ensureQueryData` loader to the Session detail route for the primary Session query so it fetches
during navigation. Secondary lists on the Session screen (the **Revision** streams) use non-awaited
prefetch so they stream in rather than block first paint. Components read via `useQuery` (never
`useLoaderData`).

The **Cleanup** event stream stays outside the cache — it is intentionally local, SSE-style component
state and does not get a loader or a `queryOptions` factory.

## Acceptance criteria

- [ ] Session detail route has a loader that awaits `ensureQueryData` for the primary Session query and
      fires non-awaited prefetch for the Revision streams.
- [ ] Components read via `useQuery`; the Cleanup event stream is untouched (still local state).
- [ ] First paint of the Session screen is not blocked on the Revision streams.

## Blocked by

- FE-002
