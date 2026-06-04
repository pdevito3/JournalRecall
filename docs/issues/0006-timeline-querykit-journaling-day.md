# 0006 — Timeline + QueryKit filters + journaling-day

**Phase:** 2 · **Type:** AFK · **Status:** done · **Realizes:** ADR-0003

## What to build

A reverse-chronological timeline of the user's Sessions, filterable via QueryKit, grouped by
**journaling day** computed in the user's own timezone.

- `GetSessionList` feature backed by **QueryKit** filtering (date range at minimum) over each
  Session's **current** state only.
- Per-user **timezone** setting (defaulted from the browser on first run); journaling-day / week /
  month membership derived by projecting the Session's UTC timestamp into that timezone.
- React: timeline list grouped by journaling day; a lightweight calendar/day jump affordance.

## Acceptance criteria

- [x] Sessions appear newest-first, grouped under their journaling day.
- [x] A Session created at 11:50pm and one at 12:10am fall on the correct days for the user's
      configured timezone (boundary test).
- [x] QueryKit date-range filtering returns the expected Sessions.
- [x] The list reflects current Session state only — no duplicate rows from historical Revisions.
- [x] Changing the timezone setting re-buckets sessions across the day boundary accordingly.

## Blocked by

- #0004
