# FE-013 — Reset the Session editor by `key` on Session identity (fix cross-Session stale text)

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

The highest-priority single fix. The Session detail editor copies server **Raw** **Draft** text into
local state via a `useEffect` behind a per-instance "hydrated" latch. Because TanStack Router reuses
the component across `/sessions/A → /sessions/B`, the editor can show one Session's text under
another's identity.

Reset the editor by `key` on Session identity at the route boundary and delete the `useEffect` +
"hydrated" latch; seed the **Raw** **Draft** local state directly from server data (valid once the
component is guaranteed fresh per Session). The debounced autosave **Draft** save model itself is out
of scope — only its *hydration* and *reset-on-navigation* are fixed.

**Decision (note in PR):** confirm whether Router reuses the Session component across param changes —
the `key` fixes the bug regardless.

## Acceptance criteria

- [ ] The Session editor remounts via `key` on Session identity; the `useEffect` + `hydrated` latch is
      deleted and local Raw Draft state is seeded directly from server data.
- [ ] Navigating `/sessions/A → /sessions/B` always shows B's text under B's heading (verified via a
      dev-browser pass).
- [ ] The debounced autosave Draft save behavior is unchanged.

## Blocked by

- None - can start immediately
