# RICH-010 — Multiple Moods

**Phase:** 13 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0006

## What to build

Let a **Session** carry more than one **Mood** — a mix of known and free-text custom Moods — instead
of a single value. Feature-independent of the Person work; it shares only the RICH-003 migration
baseline.

End-to-end behavior:

- **`Mood` value object retained**, gaining **single-string resolution**: a string matching a known
  `MoodType` name resolves to that known **Mood**, otherwise to a **custom** Mood. The literal
  `"Custom"` sentinel is never persisted.
- **Session holds a `string[]` of Moods** (value-converted / JSON column), e.g.
  `["Content", "Tired", "bittersweet"]`, **deduped** (known by key, custom by text, case-insensitive).
  Multiple customs allowed. No primary, no ordering, no provenance. Adds an incremental migration onto
  the RICH-003 baseline.
- **UI:** multi-select chips (known Moods + add-custom).
- **AI Mood Suggestions** drop the "only if none set" guard — the AI suggests any **Mood** not already
  present, deduped against the existing set, flowing through the existing `MetadataSuggestion`
  accept/reject chip flow. (Aligns with the structured Cleanup contract from RICH-004.)
- **Timeline filtering** matches a **Session** if **any** of its Moods match.

## Acceptance criteria

- [ ] `Mood` resolves a single string to known-vs-custom (no `"Custom"` sentinel persisted); the
      Session persists a deduped `string[]` of Moods (known by key, custom by text, case-insensitive),
      allowing multiple customs.
- [ ] The multi-select chip UI adds/removes known Moods and free-text custom Moods.
- [ ] AI suggests Moods not already present (even when one is set); suggestions accept/reject as chips.
- [ ] Timeline filtering returns a Session when any of its Moods matches the filter.
- [ ] Ships with unit (Mood resolution + dedupe) + integration (string[] persistence + suggestion
      dedupe) + functional (multi-mood pick) coverage.

## Blocked by

- RICH-003
