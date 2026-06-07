# RICH-012 — Markdown↔ProseMirror parity for the expanded content set

**Phase:** 14 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0006 · **Governed by:** ADR-0010

## What to build

Bring the server content converters to **parity with the expanded, markdown-expressible node/mark set**
defined in [ADR-0010](../adr/0010-markdown-expressible-content-model.md). The governing invariant: the
editor can only hold nodes/marks that `MarkdownToProseMirror` can produce, so the AI Cleanup write path
and the human editor produce the same content shapes.

End-to-end behavior (the AI Cleanup path: AI markdown → canonical JSON → derived plaintext):

- **`MarkdownToProseMirror`** is extended to emit the newly-supported set:
  - Enable Markdig `UseEmphasisExtras(Strikethrough | Marked | Inserted)` (no subscript/superscript) and
    `UseTaskLists()` on the pipeline.
  - The emphasis handler **branches on the delimiter character** rather than only counting delimiters:
    `*`/`_` → bold/italic, `~~` → `strike`, `==` → `highlight`, `++` → `underline`.
  - **Thematic breaks (`---`)** now emit a `horizontalRule` node (previously dropped).
  - **Links** emit a `link` mark on their visible text (previously degraded to plain text).
  - **Task list items (`- [ ]` / `- [x]`)** emit `taskList`/`taskItem` nodes carrying the checked state.
- **`ProseMirrorToPlainText`** handles the new nodes: `taskItem` renders its text only (no checkbox
  glyph, consistent with how list bullets are dropped); `horizontalRule` contributes nothing. Unknown
  input still never throws.
- Single-color `highlight` only — `==text==` carries no color, so no color attribute is emitted or read.

Pure modules, no DB or network. No EF/schema change — content stays a JSON string column.

## Acceptance criteria

- [ ] `MarkdownToProseMirror` converts `~~`/`==`/`++` to `strike`/`highlight`/`underline` marks, `---`
      to `horizontalRule`, `[text](url)` to a `link` mark, and `- [ ]`/`- [x]` to `taskList`/`taskItem`
      with correct checked state.
- [ ] Markdig subscript/superscript stay literal text (only Strikethrough/Marked/Inserted enabled).
- [ ] `ProseMirrorToPlainText` renders `taskItem` text and omits `horizontalRule`; empty/whitespace/
      null/malformed input still yields `""` and never throws.
- [ ] Every node/mark in the ADR-0010 set (except the `mention` atom) is producible by
      `MarkdownToProseMirror` from some markdown input — the parity invariant holds.
- [ ] Ships with extended unit coverage for both modules.

## Blocked by

- None - can start immediately
