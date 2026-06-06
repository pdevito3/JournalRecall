# FE-016 — Derive the timezone default at render, not via a write effect

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

The timeline currently auto-persists a timezone default from a render effect, so opening the timeline
can fire a surprise settings write (or fire it twice). Derive the timezone default at render time and
persist only on an explicit user action (or behind a one-shot guard), so viewing the timeline never
mutates settings as a side effect of mounting.

## Acceptance criteria

- [ ] The timezone default is computed at render; no render effect auto-mutates settings on mount.
- [ ] The settings write fires only on explicit user action (or a single guarded one-shot), never twice.
- [ ] Existing timezone behavior (the picker, the journaling-day projection) is unchanged for the user.

## Blocked by

- None - can start immediately
