# FE-009 — Timeline Topic/Person/Mood filters as URL search state

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

Move the timeline's **Topic** / **Person** / **Mood** filters out of `useState` and into URL search
state via `validateSearch` (zod) + `loaderDeps`, so a filtered view of the journal is shareable,
bookmarkable, refresh-surviving, and back-button-able. Malformed search params are normalized to
defaults by the zod schema instead of crashing the route. `loaderDeps` selects only the params that
affect the query, so the loader re-runs only when those inputs change.

## Acceptance criteria

- [ ] Timeline filters live in the URL via `validateSearch`; invalid/missing params fall back to
      defaults rather than throwing.
- [ ] `loaderDeps` feeds the loader only the filter params that affect the query; the loader keys its
      query off the validated deps.
- [ ] A unit test covers the timeline search schema: valid params parse, invalid/missing fall back to
      defaults, inferred type matches.

## Blocked by

- FE-002
