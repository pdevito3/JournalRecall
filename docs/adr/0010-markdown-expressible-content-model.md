# Markdown-expressible content model with a full formatting toolbar

## Status

accepted — expands the node/mark set locked in [ADR-0009](0009-prosemirror-json-content-model.md)

## Context & decision

[ADR-0009](0009-prosemirror-json-content-model.md) established canonical ProseMirror/tiptap JSON as the
stored **Content** representation with a small, explicitly **locked** node/mark set, reachable only
through StarterKit's markdown input rules ("no toolbar is required"). In practice the input-rule-only
affordance is too discoverable-by-accident: Notion-style slash commands aren't well supported in tiptap,
and writers expect a visible formatting bar. We are adding a full formatting **toolbar** to the editing
surfaces and expanding the supported set to cover strikethrough, underline, highlight, task lists
(todos), horizontal rules, and links.

Expanding the set forces a question ADR-0009 didn't have to answer: *which* formatting is allowed? We
adopt a single governing rule:

> **The supported node/mark set is exactly what markdown can express — i.e. exactly what
> `MarkdownToProseMirror` can produce.** Nothing in the editor can hold a node or mark the AI Cleanup
> path could never generate.

This keeps the two write paths — the human editor and the AI Cleanup converter — producing the *same*
content shapes. There are no "human-only" orphan nodes that round-trip oddly, fail to appear in
AI-cleaned copy, or surprise a future reader of the JSON.

### The supported set

- **Marks:** `bold`, `italic`, `code`, `strike`, `underline`, `highlight` (single-color), `link`.
- **Nodes:** `doc`, `paragraph`, `heading` (1–3), `bulletList`, `orderedList`, `listItem`,
  `taskList`, `taskItem`, `blockquote`, `codeBlock`, `horizontalRule`, and the `mention` inline atom.

`strike`, `underline`, `horizontalRule`, and `link` already ship in tiptap v3's StarterKit (they were
disabled by ADR-0009) — they are simply re-enabled. `highlight` (`@tiptap/extension-highlight`) and
`taskList`/`taskItem` (`@tiptap/extension-list`) are added.

### Markdown dialect (the parity contract)

`MarkdownToProseMirror` is brought to parity with the set above by enabling, on the Markdig pipeline:

- `UseEmphasisExtras(Strikethrough | Marked | Inserted)` — `~~strike~~`, `==highlight==` (marked),
  `++underline++` (inserted). Subscript/superscript are **not** enabled (we don't support them).
- `UseTaskLists()` — `- [ ]` / `- [x]`.

The emphasis handler now branches on the delimiter **character** (`*`/`_` → bold/italic, `~~` → strike,
`==` → highlight, `++` → underline) rather than only counting delimiters. Thematic breaks (`---`), which
ADR-0009's converter dropped, now emit a `horizontalRule`; links, previously degraded to plain text, now
emit a `link` mark.

### `mention` is the one sanctioned non-markdown node

`mention` has no markdown form, but it is the app's own inline atom (RICH-007), produced by the editor's
`@`-autocomplete and by server-side `MentionInsertion` — never by `MarkdownToProseMirror`, and never
expected from the AI. It predates this invariant and is the deliberate, documented exception.

## Invariants preserved

- The **JSON-canonical / derived-plaintext** architecture of [ADR-0009](0009-prosemirror-json-content-model.md)
  is unchanged: Content is still stored JSON, `ProseMirrorToPlainText` still feeds search and the AI
  Cleanup input. It is extended only to render `taskItem` text (like list items, no checkbox glyph) and
  to ignore `horizontalRule`.
- **No schema/DB change.** Content remains a JSON string column; new nodes/marks are new JSON shapes, not
  new columns. No EF migration, no DB drop.
- The editor stays **uncontrolled with the keyed-remount pattern**; the toolbar is presentational over
  the same editor instance and renders only when `editable` (Raw + Cleaned surfaces, never the read-only
  Revision drill-down).

## Considered options

- **Text alignment (left/center/right).** Originally requested, but **rejected**: no markdown dialect can
  express it, so it would be a human-only orphan attribute the AI could never reproduce — exactly what the
  parity invariant exists to prevent.
- **Multicolor highlight.** **Rejected** for the same reason: `==text==` carries no color, so a color
  attribute would be non-markdown-expressible. Highlight is single-color.
- **A richer editor-only set, no parity rule.** Maximizes formatting options but reintroduces the
  human/AI content-shape divergence; rejected in favor of the simpler, coherent invariant.

## Consequences

- The set is now governed by a rule, not a hand-maintained list: to add a formatting feature, it must
  first be markdown-expressible and wired through `MarkdownToProseMirror`. The converter's unit suite is
  the enforcement point.
- ADR-0009's "locked set" and its "no toolbar is required" rationale are **superseded**; the
  JSON-content-model decision itself stands.
- Markdig's emphasis-extras delimiters (`~ = +`) now carry meaning in AI output. The selective
  `EmphasisExtraOptions` (no sub/superscript) keeps `~single~` and `^carets^` as literal text.
