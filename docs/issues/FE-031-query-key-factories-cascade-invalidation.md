# FE-031 — Query-key factories + cascade invalidation

**Phase:** 14 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

Replace the ad-hoc, inline query-key string-array literals scattered across the feature hooks with
**per-feature key factories** following the tkdodo "effective query keys" pattern
(https://tkdodo.eu/blog/effective-react-query-keys). Each feature gets a colocated factory (a `keys.ts`
in the feature folder, importable by cross-feature consumers without pulling in the whole hooks module)
exposing a generic-to-specific hierarchy rooted at one `all` key.

End-to-end behavior:

- **Sessions** get the full hierarchy: `all → lists()/list({ filter, mood }) → details()/detail(id) →
  revisions(id)/cleanedRevisions(id)` and the per-revision keys. The singular `session` detail is
  **unified under the `sessions` root** so a single invalidate cascades to lists, details, and revision
  streams via prefix matching.
- **Invalidation becomes targeted, leaning on the cascade:** session mutations invalidate
  `detail(id)` (which already covers that session's revision/cleaned-revision streams) plus `lists()`
  for the timeline, and only hit the genuinely separate roots (`topics`, `people`) when relevant. The
  current redundant double-invalidates (e.g. invalidating both `['session', id, 'revisions']` and
  `['session', id]`) are dropped.
- **The list key uses an object param** `list({ filter, mood })` instead of positional `null`s.
- **Cross-feature invalidations import the owning feature's factory:** `useSettings` invalidates
  `sessionKeys.all` (not a re-typed `['sessions']`), `useAdmin` invalidates `authKeys.config`, etc.
- **Trivial features** (auth, admin, settings, summaries, corrections) each get a small factory too,
  for consistency — even single-key ones.
- **Tests** that hardcode keys (`['me']`, `['auth', 'config']`, …) are updated to import the factories.

This is a pure frontend refactor: no API, schema, or DTO changes, and the resulting cache keys must be
equivalent in coverage to today's (the cascade may invalidate *more* deliberately, never less).

## Acceptance criteria

- [ ] Every feature query key is produced by a colocated key factory; no inline key array literals
      remain in hooks or tests.
- [ ] Sessions use the `all → lists/list → details/detail → revisions` hierarchy with the singular
      detail rooted under `sessions`; `invalidateQueries(sessionKeys.all)` cascades to lists, details,
      and revision streams.
- [ ] Session mutations use targeted cascade invalidation; the previously redundant explicit invalidates
      are removed; `topics`/`people` are still invalidated where the mutation can affect them.
- [ ] Cross-feature invalidations import the owning factory rather than re-typing the literal.
- [ ] App boots (Aspire 5247/7247/4247), all screens fetch and refresh correctly after mutations, and
      the existing FE test suite stays green (key-referencing tests updated to the factories).

## Blocked by

- None - can start immediately
