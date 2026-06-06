# FE-004 — Optional `select` parameter on the shared `me` hook

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

Give the shared `me` hook an optional `select` parameter (factory options + `select`) so a component
can subscribe to just the slice it needs (e.g. `username`, `mustChangePassword`, roles) and re-render
only when that slice changes. Reimplement the `useIsAdmin`/`useAuthRoles` derived hooks from FE-003 on
top of this parameter.

Apply `select` only where a real slice / over-render exists. Do **not** add `select` to hooks that
legitimately render whole payloads (the timeline list, Session detail) — that is explicitly avoided as
premature.

## Acceptance criteria

- [ ] `useMe` accepts an optional `select` and forwards it to the factory-backed `useQuery`.
- [ ] `useIsAdmin`/`useAuthRoles` are implemented via the `select` parameter.
- [ ] No `select` added to whole-payload hooks (timeline list, Session detail).

## Blocked by

- FE-003
