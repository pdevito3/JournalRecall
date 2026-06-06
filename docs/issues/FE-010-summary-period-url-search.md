# FE-010 — Summary period/date as URL search state

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

Move the **Summary** period (`Day | Week | Month | Quarter | Year`) and anchor date out of `useState`
and into URL search state via `validateSearch` (zod) + `loaderDeps`, so a specific period roll-up is a
shareable, refresh-surviving link. Malformed params normalize to defaults via the zod schema;
`loaderDeps` re-runs the loader only when period/date change.

## Acceptance criteria

- [ ] Summary period + anchor date live in the URL via `validateSearch`; invalid/missing params fall
      back to defaults.
- [ ] `loaderDeps` feeds the loader the validated period/date; the loader keys its query off them.
- [ ] A unit test covers the Summary search schema: valid params parse, invalid/missing fall back to
      defaults, inferred type matches.

## Blocked by

- FE-002
