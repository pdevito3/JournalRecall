# 0029 — Filter the timeline by Activity

**Type:** AFK · **Status:** ready-for-agent · **Realizes:** [PRD-0007](../prd/0007-session-activity-metadata.md)

## Parent

[PRD-0007 — Session Activity metadata](../prd/0007-session-activity-metadata.md)

## What to build

Let the **User** filter the journal timeline by **Activity** — "show me everything I wrote while
walking." Builds on the Activity captured in #0028.

- `GET /api/sessions` gains a single-select **Activity** filter facet, combinable with the existing
  **Mood**/**Topic** filters (e.g. anxious entries written while commuting).
- The timeline UI gains an Activity filter control alongside the existing facets.

## Acceptance criteria

- [ ] The timeline can be filtered to Sessions with a selected Activity, returning only matching
      Sessions.
- [ ] The Activity filter combines with Mood/Topic filters.
- [ ] The filter control sits alongside the existing Mood/Topic controls in the timeline UI.
- [ ] **Functional test**: a filtered timeline returns only Sessions with the selected Activity, and
      combines correctly with a Mood/Topic facet.

## Blocked by

- #0028 — Capture & persist a Session Activity
