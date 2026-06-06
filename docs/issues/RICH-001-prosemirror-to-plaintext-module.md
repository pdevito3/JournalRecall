# RICH-001 — `ProseMirrorToPlainText` deep module

**Phase:** 13 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0006

## What to build

The pure projection that turns canonical **Content** JSON into the derived plaintext used by the
search index and as the AI **Cleanup** input. Nothing correctness-critical should ever parse the
JSON directly — this module is the one seam from rich representation back to words.

Given a ProseMirror/tiptap document (the small node/mark set: paragraph, headings, bold/italic,
lists, blockquote, code, mention), produce a faithful plaintext rendering: block nodes separate with
newlines, list items render their text, marks are stripped, and `mention` nodes render their display
`label`. Pure and deterministic — no I/O, no DB.

## Acceptance criteria

- [ ] A pure module converts a canonical Content JSON document to plaintext, covering paragraphs,
      headings, bold/italic, ordered/unordered (incl. nested) lists, blockquote, and code.
- [ ] `mention` nodes render their `label` text in the plaintext output.
- [ ] Empty/whitespace-only documents project to empty (or whitespace) text without throwing.
- [ ] Unit tests assert input→output across the node set, nested lists, and mention rendering.

## Blocked by

- None - can start immediately
