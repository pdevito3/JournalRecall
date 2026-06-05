# 0012 — AI metadata Suggestions (accept/reject)

**Phase:** 5 · **Type:** AFK · **Status:** done

## What to build

During Cleanup, AI proposes metadata as **Suggestions** the user accepts or rejects. Accepted
Suggestions become regular metadata (provenance `AiSuggested`); user-set metadata is never
overwritten.

- Cleanup emits Topic/Person/Mood **Suggestions** alongside the Cleaned copy and Synopsis.
- Accept promotes a Suggestion to metadata with provenance `AiSuggested`; reject discards it.
- AI never overwrites or removes `UserSet` metadata.
- React: suggestion chips with accept/reject on the Session.

## Acceptance criteria

- [x] A Cleanup run yields metadata Suggestions distinct from accepted metadata.
- [x] Accepting a Suggestion adds it as metadata with provenance `AiSuggested`; rejecting removes it
      from the suggestion list.
- [x] A Topic/Person/Mood the user already set as `UserSet` is not overwritten or duplicated by AI.
- [x] Suggestions are scoped to the owning user only.
- [x] Tests cover accept, reject, and the no-overwrite-of-UserSet rule.

## Blocked by

- #0008
- #0011
