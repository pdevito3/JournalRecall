# RICH-011 — Topic badges

**Phase:** 13 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0006

## What to build

Turn **Topic** tagging from a comma-separated text box into a badge picker with autocomplete over
Topics the **User** has used before. Unlike **Person**, there is **no Topic directory entity** —
Topics stay owned `SessionTopic` strings (a separate badge surface, never inline, never id-referenced).
Feature-independent of the Person/Mood work; shares only the RICH-003 migration baseline.

End-to-end behavior:

- **Badge picker UI:** Topics shown and edited as chips/badges with autocomplete; adding an unknown
  Topic just creates a new `SessionTopic` (never blocked from coining a new tag).
- **`GET /topics`** returns the **distinct Topic names** across the User's **Sessions**, powering badge
  autocomplete.
- **Indices:** confirm `Session(UserId)`; add `SessionTopic(SessionId, Name)` so the join + distinct
  runs off the index. Denormalizing `UserId` onto `SessionTopic` is explicitly deferred unless
  profiling demands it. Adds an incremental migration onto the RICH-003 baseline.
- **AI Topic Suggestions are unchanged** — they keep arriving as accept/reject chips through the
  existing `MetadataSuggestion` flow (sourced from the structured Cleanup contract, RICH-004).

## Acceptance criteria

- [ ] Topics are shown/edited as badges with autocomplete; adding an unknown Topic creates a new
      `SessionTopic`.
- [ ] `GET /topics` returns the User's distinct Topic names and is backed by the
      `SessionTopic(SessionId, Name)` index (with `Session(UserId)` confirmed).
- [ ] AI Topic Suggestions still arrive as accept/reject chips (flow unchanged).
- [ ] Ships with unit + integration (`GET /topics` distinct + index) + functional (badge pick/add)
      coverage.

## Blocked by

- RICH-003
