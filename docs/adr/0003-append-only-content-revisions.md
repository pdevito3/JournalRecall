# Session content is append-only Revisions; everything else is mutable

## Status

accepted

## Context & decision

A Session holds two bodies of text — **Raw** (human-owned, never altered by AI) and **Cleaned**
(AI-derived, also user-editable). Both are user-editable, so a re-run of AI Cleanup can overwrite
hand-edited Cleaned text. To never lose words, **content edits append an immutable Revision rather
than overwriting**. Raw and Cleaned each get their **own Revision stream**. A new Revision is
minted only at **save points** (AI-run completion, explicit/debounced edit saves) — not per
keystroke; the live Draft mutates until it crystallizes. **Everything else** about a Session
(status, metadata, mood, location) stays ordinary mutable state — this is a revisions table, **not**
full event-sourcing of the aggregate.

## Considered options

- **Mutable single content field** — simplest, but a re-run silently destroys the user's polish and
  there is no edit history.
- **Full event-sourcing of the Session** — maximum auditability, but a large complexity tax that
  fights the SQLite + simplicity goals; metadata changes don't need an event log.

## Consequences

- Re-running Cleanup over hand-edited Cleaned text is **warn-and-overwrite with the prior Revision
  retained** — recoverable, never silently lost.
- **Revisions are a per-Session/day drill-down, not part of the searchable index.** Browsing and
  filtering query only each Session's current state; historical Revisions never appear as separate
  results. This keeps the index free of duplicate hits.
