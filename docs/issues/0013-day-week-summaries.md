# 0013 — Day & Week Summaries (on-demand)

**Phase:** 6 · **Type:** AFK · **Status:** todo

## What to build

The first **Summary** tier: AI-generated **Day** and **Week** summaries built directly from the
user's Sessions, generated on demand from the summary page. No scheduler.

- `Summary` aggregate keyed by (user, period, date); period `Day | Week`.
- Day and Week summarize the underlying Sessions directly, reading the **Cleaned copy when present,
  else Raw**. Week is a parallel roll-up of its days' Sessions (spans month boundaries).
- Generation triggered on viewing the summary page when missing/Stale, plus an explicit **Refresh**;
  a "generating…" state while running.
- React: summary page with Day and Week views.

## Acceptance criteria

- [ ] Opening a Day with Sessions generates and displays its Summary; same for a Week.
- [ ] A Summary reads the Cleaned copy when a Session has one, else the Raw text (verified by
      cleaning one Session in the period and checking the source used).
- [ ] A Week spanning two months rolls up the correct days regardless of month boundary.
- [ ] Refresh regenerates the Summary; a generating state is shown during the run.
- [ ] Summaries are per-user and private (no cross-user access).
- [ ] No background scheduler is introduced — generation is user-triggered only.

## Blocked by

- #0007
- #0004
