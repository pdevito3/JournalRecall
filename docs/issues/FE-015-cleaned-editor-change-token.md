# FE-015 — Simplify the Cleaned editor reconciler to `key` + change-token

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

Replace the **Cleaned** editor's manual ref/effect reconciler with a `key` on Session identity + the
cleaned **Revision** change-token, preserving the intent that local unsaved edits win until a save
point, and that a server regeneration (a **Cleanup** re-run) re-seeds the editor rather than silently
clobbering or stranding hand-edits.

**Decisions (record in the issue/PR):** identify the canonical change-token on the Session DTO for
keying the Cleaned editor; confirm the "what wins on a concurrent server change with unsaved edits"
policy.

## Acceptance criteria

- [ ] The Cleaned editor resets via `key` on Session identity + the cleaned-Revision change-token; the
      manual ref/effect reconciler is removed.
- [ ] Unsaved local edits are preserved until a save point; a server regeneration re-seeds the editor.
- [ ] The canonical change-token used for keying, and the concurrent-edit policy, are documented in the
      PR.

## Blocked by

- FE-013
