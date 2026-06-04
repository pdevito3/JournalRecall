# 0005 — Raw Revision history

**Phase:** 2 · **Type:** AFK · **Status:** done · **Realizes:** ADR-0003

## What to build

Edits to a Session's Raw text **append** an immutable **Revision** at save points rather than
overwriting, giving every Session a full Raw edit history that the user can browse from within the
Session.

- Append a Raw **Revision** on explicit/debounced save (not per keystroke); the live Draft mutates
  until it crystallizes.
- Per-Session **Revision history** drill-down in the UI (timeline of Raw versions, view a past
  version). Revisions are **not** part of any list/search index.

## Acceptance criteria

- [x] Saving an edit creates a new Raw Revision; the prior Revision remains unchanged and viewable.
- [x] Rapid keystrokes do not each create a Revision — only the debounced/explicit save does.
- [x] The Session view shows a Revision history and can render a selected past Raw Revision.
- [x] Historical Revisions do not appear as separate entries in the timeline/list from #0006.
- [x] Tests assert append-only behavior (count grows, old content immutable) at save points.

## Blocked by

- #0004
