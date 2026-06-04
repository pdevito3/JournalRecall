# 0009 — Corrections

**Phase:** 4 · **Type:** AFK · **Status:** todo · **Realizes:** ADR-0003

## What to build

A per-user **Corrections** list that fixes mis-dictations during Cleanup. Each Correction has a
canonical term and common mishearings, with a default AI-context-hint mode and an optional
**hard-replace** mode. Applied only to the Cleaned copy.

- Per-user Corrections CRUD (canonical term + mishearings + hard-replace flag).
- During Cleanup, the Corrections list is injected into the prompt as context (AI fixes in-context);
  hard-replace entries are substituted deterministically.
- React: Corrections management page.

## Acceptance criteria

- [ ] A user can create/edit/delete Corrections scoped to themselves only.
- [ ] With a Correction `Profisee` ← `prophecy` (hint mode), a Cleanup of Raw containing "prophecy"
      (as the company) yields "Profisee" in the Cleaned copy while Raw is unchanged.
- [ ] A hard-replace Correction substitutes every occurrence deterministically.
- [ ] Corrections never alter Raw — only the Cleaned copy.
- [ ] Another user's Corrections are not visible or applied (per-user isolation test).

## Blocked by

- #0008
