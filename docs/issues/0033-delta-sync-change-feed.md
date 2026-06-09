# 0033 — Delta sync change feed with cursor

**Phase:** 9 (mobile sync) · **Type:** AFK · **Status:** ready · **Realizes:** ADR-0013 · **Paired with:** journal-recall-ios#0006

## What to build

A pull-based change feed so an offline-first client converges cheaply: `GET /api/sync/changes?since=<cursor>`
returns everything of the caller's that changed since the cursor, plus the next cursor. Backed by
an `UpdatedAt` column (touched on every mutation, including Cleanup completion and suggestion
changes) on the synced entities: Sessions (full current state — raw/cleaned content, synopsis,
cleanup status, metadata, suggestions, people proposals), Corrections, People, and user Settings.
The cursor is opaque to the client and monotonic; an empty `since` means "from the beginning"
(first-sync bootstrap). Strictly tenant-scoped like every other endpoint.

## Acceptance criteria

- [ ] Mutating a Session (draft save, cleanup completion, metadata, suggestion accept) makes it
      appear in the next `changes` pull; unchanged entities do not.
- [ ] Corrections, People, and Settings changes appear in the feed.
- [ ] The returned cursor, replayed, yields only changes made after it (no gaps, no repeats under
      sequential writes).
- [ ] Calling with no cursor returns the user's full state (bootstrap).
- [ ] Another user's changes never appear (integration test).

## Blocked by

None — can start immediately.
