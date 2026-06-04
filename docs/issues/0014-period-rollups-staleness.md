# 0014 — Month/Quarter/Year roll-ups + staleness propagation

**Phase:** 6 · **Type:** AFK · **Status:** todo

## What to build

The hierarchical Summary tiers and staleness propagation: **Month** summarizes its Day Summaries,
**Quarter** summarizes its Months, **Year** summarizes its Quarters — and any change beneath a
Summary marks it (and its ancestors) **Stale**.

- Extend the `Summary` aggregate to periods `Month | Quarter | Year` generated from the level below.
- Staleness propagates up the chain when an underlying Session (or lower Summary) changes.
- React: Month/Quarter/Year views with Stale + Refresh affordances.

## Acceptance criteria

- [ ] A Month Summary is generated from its Day Summaries; Quarter from Months; Year from Quarters.
- [ ] Editing a Session marks its Day, Month, Quarter, and Year Summaries (and its Week) Stale.
- [ ] Refreshing a higher period regenerates from the current lower-level summaries.
- [ ] Viewing a Stale Summary offers regeneration; a fresh one does not.
- [ ] Tests assert the roll-up source per level and upward staleness propagation.

## Blocked by

- #0013
