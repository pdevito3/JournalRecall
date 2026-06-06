# RICH-004 — Cleanup structured output contract + AI markdown→JSON Cleaned

**Phase:** 13 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0006

## What to build

Make AI-produced **Cleaned** **Content** render with real formatting, and restructure the **Cleanup**
output into a single structured object that carries prose plus all metadata side-channels.

End-to-end behavior:

- **Cleanup output contract** becomes a structured object:
  `{ cleanedMarkdown, topicSuggestions[], moodSuggestions[], peopleProposal[] }`. Prose is markdown;
  everything else is a structured side-channel. (`peopleProposal` is consumed in RICH-009;
  `moodSuggestions`/`topicSuggestions` feed RICH-010/011; this slice just establishes the contract.)
- **Server converts** `cleanedMarkdown` to canonical Content JSON via `MarkdownToProseMirror`
  (RICH-002) and stores JSON, so the AI **Cleaned** copy renders rich in the editor. A User's own edits
  remain pure JSON round-trips with no conversion.
- **AI still never touches Raw**, and Cleanup keeps reading the derived plaintext (RICH-001), not the
  JSON markup — AI quality is unaffected by the rich representation.

## Acceptance criteria

- [ ] `CleanupAgent` (and its callers) return the structured object
      `{ cleanedMarkdown, topicSuggestions[], moodSuggestions[], peopleProposal[] }`.
- [ ] The server converts `cleanedMarkdown` to canonical Content JSON and persists it; a Cleanup run
      produces a Cleaned copy that renders with formatting in the editor and snapshots a Revision.
- [ ] Cleanup input is the derived plaintext of Raw; Raw is never modified.
- [ ] Integration tests cover the new output contract shape and the markdown→JSON persistence of
      Cleaned; ships with unit + integration coverage.

## Blocked by

- RICH-002
- RICH-003
