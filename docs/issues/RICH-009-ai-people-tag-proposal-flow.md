# RICH-009 — AI people-tag proposal flow + `RequirePeopleTagApproval`

**Phase:** 13 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0006

## What to build

Let AI **Cleanup** propose which **People** to tag, with a per-Person review the **User** approves,
inserting approved tags deterministically — the AI never silently writes to the **People** directory
or rewrites prose.

End-to-end behavior:

- **People leave the shared `MetadataSuggestion` machinery** — `SuggestionKind.Person` is **removed**
  into a dedicated **people-tag proposal**. Topics/Moods keep the chip/`MetadataSuggestion` model.
- Cleanup's `peopleProposal[]` (from RICH-004) carries, per candidate, `{ label, resolution hint,
  context spans }`. The AI is given the User's **Person** directory as context to favor reuse.
- **Resolution:** deterministic exact/alias matches (`PersonResolver`, RICH-006) auto-link to an
  existing **Person**; non-matches are proposed as **"new"**; fuzzy/low-confidence targets are
  AI-proposed but **must be User-confirmed** — the AI never silently binds a fuzzy match.
- **Review card, per-Person:** shows every context span the AI would tag them in; lets the User
  approve/reject the **whole** Person, **reassign** to a different existing Person, or force
  **create-new**; a brand-new Person is clearly badged **"new."** (Per-instance exclusion is done
  afterward by editing the prose — mentions are live nodes.)
- **On approve:** `MentionInsertion` (RICH-008) wraps the approved spans in mention nodes in the
  Cleaned copy — **no second AI pass**. New People are upserted to the directory only on approval.
- **`UserSettings.RequirePeopleTagApproval`** defaults **true**. When **false**, resolved mentions are
  inserted inline automatically at Cleanup time and new People upserted immediately.

## Acceptance criteria

- [ ] `SuggestionKind.Person` is removed from the shared suggestion machinery; Topics/Moods still flow
      through `MetadataSuggestion`.
- [ ] After Cleanup, proposed People appear per-Person with their context-span previews; exact/alias
      matches auto-link to existing People, non-matches show as "new," fuzzy targets require confirmation.
- [ ] The User can approve/reject per Person, reassign to a different existing Person, or force create-new;
      approving inserts mentions exactly at the proposed spans with no further AI prose rewriting.
- [ ] `UserSettings.RequirePeopleTagApproval` defaults true; with it false, resolved mentions and new
      People are applied automatically at Cleanup time.
- [ ] Ships with unit + integration (approve flow + both `RequirePeopleTagApproval` branches) +
      functional (proposal review/approve) coverage.

## Blocked by

- RICH-004
- RICH-007
- RICH-008
