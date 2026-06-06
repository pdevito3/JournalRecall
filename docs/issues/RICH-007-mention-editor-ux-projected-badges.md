# RICH-007 — `@`-mention editor UX + projected People badges

**Phase:** 13 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0006

## What to build

The manual `@`-mention experience: tag **People** by mentioning them inline in either editor, with the
**People** **Metadata** badges as a read-only projection of who's mentioned.

End-to-end behavior:

- Typing `@` in **either** the Raw or Cleaned editor opens an autocomplete over the User's **People**
  directory (`GET /people`). Picking inserts a `mention` node `{ personId, label }`.
- Autocomplete shows **People referenced before** so the User reuses existing entries; a User can
  **create a new Person inline** (`POST /people`) without leaving the editor, then mention them.
- The **People badges** are read-only output, reconciled on every save via `MentionProjection`
  (RICH-006) as the union of Raw + Cleaned mentions — never edited directly.
- **Removing** an `@`-mention from the prose removes that **Person** from the **Metadata** (one obvious
  way to untag).
- **Renaming** a Person in the directory updates every mention's displayed label (mentions reference
  `personId`).

## Acceptance criteria

- [ ] Typing `@` in either editor shows directory autocomplete; selecting inserts a mention node and
      the Person appears in the read-only People badges after save.
- [ ] A new Person can be created inline from the `@` flow and immediately mentioned.
- [ ] Badges reflect exactly the union of Raw + Cleaned mentions; removing a mention untags the Person.
- [ ] Renaming a Person in the directory updates the label shown at existing mention sites.
- [ ] Ships with functional coverage of the create/pick + projected-badge flow (tiptap mention wiring
      covered here, not via unit tests), plus integration coverage of save-time reconciliation.

## Blocked by

- RICH-006
