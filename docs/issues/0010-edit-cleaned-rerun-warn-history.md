# 0010 — Edit Cleaned + re-run warn-and-overwrite + history

**Phase:** 4 · **Type:** AFK · **Status:** done · **Realizes:** ADR-0003

## What to build

Let the user hand-edit the **Cleaned** copy, and make a Cleanup re-run safe: warn before
overwriting hand-edits, retain the prior Cleaned **Revision**, and expose the Cleaned history.

- Cleaned copy is user-editable; edits append to the Cleaned Revision stream.
- Re-running Cleanup over a hand-edited Cleaned copy **warns/confirms** before regenerating; on
  confirm it overwrites but keeps the previous Cleaned Revision.
- React: edit Cleaned; confirmation dialog on re-run when hand-edits exist; Cleaned Revision history
  drill-down.

## Acceptance criteria

- [x] The user can edit the Cleaned copy and the edit is saved as a Cleaned Revision.
- [x] Re-running Cleanup when the Cleaned copy has hand-edits prompts a confirm before overwriting.
- [x] After confirming a re-run, the prior (hand-edited) Cleaned Revision is still retrievable from
      history.
- [x] Raw is never affected by editing Cleaned or by re-running.
- [x] Re-running when there are **no** hand-edits does not prompt.

## Blocked by

- #0008
