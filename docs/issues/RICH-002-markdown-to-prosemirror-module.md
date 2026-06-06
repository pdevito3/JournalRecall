# RICH-002 — `MarkdownToProseMirror` deep module

**Phase:** 13 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0006

## What to build

The server-side conversion from AI-emitted **markdown** to canonical **Content** JSON. The AI is
never asked to produce schema-valid editor JSON; it emits markdown and the server converts. A
**User**'s own edits stay pure JSON round-trips and never touch this path — conversion applies to
AI-generated content only.

Parse markdown (Markdig is the expected parser) and emit a ProseMirror/tiptap document restricted to
the small node/mark set (paragraph, headings, bold/italic, ordered/unordered lists, blockquote,
code). Unsupported markdown constructs degrade to the nearest supported node rather than throwing.
Pure and deterministic — no DB, no network. Mention insertion is **not** part of this module (that is
RICH-008).

## Acceptance criteria

- [ ] A pure module converts markdown to canonical Content JSON over the supported node/mark set.
- [ ] Output JSON validates against the same small schema consumed by `ProseMirrorToPlainText`
      (RICH-001), so a markdown→JSON→plaintext round-trip preserves the words.
- [ ] Unsupported/unknown markdown degrades gracefully (nearest supported node) without throwing.
- [ ] Unit tests assert input→output across the node set, including nested lists and inline marks.

## Blocked by

- None - can start immediately
