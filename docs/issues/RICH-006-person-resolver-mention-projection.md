# RICH-006 — `PersonResolver` + `MentionProjection` deep modules

**Phase:** 13 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0006

## What to build

The two server modules that bind mention nodes to directory **People** and keep the **People**
**Metadata** a pure projection of the prose.

- **`PersonResolver`** (repo-backed): given a detected name, return the existing `PersonId` via
  deterministic **exact / alias** match against the User's directory, else signal "new". Single source
  of truth for name→Person resolution; used by both the manual mention path (RICH-007) and the AI
  proposal (RICH-009).
- **`MentionProjection`** (pure): given a tiptap document, produce the set of `PersonId`s referenced by
  its `mention` nodes. On save it reconciles a **Session**'s `SessionPerson` references — **unioned
  across Raw + Cleaned** — so the People badges never drift from the prose. Mention nodes carry
  `{ personId, label }`: `personId` is the durable link, `label` a display snapshot.

The save-time reconciliation wiring is exercised here; the editor UX that produces mentions is
RICH-007.

## Acceptance criteria

- [ ] `PersonResolver` returns the matching `PersonId` for an exact (and alias) name match and signals
      "new" otherwise; resolution is per-User.
- [ ] `MentionProjection` extracts the `PersonId` set from a document and reconciles a Session's
      `SessionPerson` refs as the **union of Raw + Cleaned** mentions (adds new, removes absent).
- [ ] Removing a mention from one copy but keeping it in the other keeps the Person (union semantics).
- [ ] Unit tests cover exact-vs-alias-vs-new resolution and the Raw+Cleaned union/reconcile, including
      add and remove.

## Blocked by

- RICH-005
