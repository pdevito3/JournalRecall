# ProseMirror JSON content model with a derived plaintext projection

## Status

accepted

## Context & decision

Realizes [PRD-0006](../prd/0006-rich-editor-people-directory-multi-mood-topic-badges.md) and is the
keystone slice [RICH-003](../../docs/issues/RICH-003-rich-editor-json-content-model.md). Until now a
Session's **Raw** and **Cleaned** **Content** (CONTEXT.md) were plain strings edited in `<textarea>`s.
PRD-0006 wants a Notion-style rich editor, @-mentions, and richer metadata — none of which a flat string
can carry. We need a content representation that is structured enough for formatting and inline mentions,
yet still feeds plain text to the things that only want words (search, the AI Cleanup prompt, the
timeline preview).

We adopt **canonical ProseMirror/tiptap JSON as the stored Content representation, with a derived
plaintext projection persisted alongside it**.

- **Canonical representation.** Both `Session.RawDraft` and `Session.CleanedDraft` hold a serialized
  ProseMirror/tiptap document (a small, locked node/mark set: `doc`, `paragraph`, `heading` levels 1–3,
  `bulletList`, `orderedList`, `listItem`, `blockquote`, `codeBlock`; marks `bold`, `italic`, `code`;
  and a `mention` inline atom reserved for RICH-007). The existing string columns are reused — **the DTO
  and wire contract are unchanged**: `rawDraft`/`cleanedDraft` stay string fields that now carry
  serialized JSON. Never-written content is `""`, which the editor and the projection both treat as an
  empty document.

- **Derived plaintext projection.** The aggregate recomputes a plain-text rendering on **every** save
  point — `SaveDraft`, `CompleteCleanup`, `EditCleaned` — into two new persisted columns,
  `RawPlainText` and `CleanedPlainText`, via the pure `ProseMirrorToPlainText` module
  ([RICH-001](../../docs/issues/RICH-001-prosemirror-to-plaintext-module.md)). These — not the JSON
  markup — are what the timeline preview, the QueryKit `raw` word-search query name, and the AI Cleanup
  input read. Formatting therefore never hides content from search, and AI quality is unaffected by the
  rich representation.

- **The projection is derived, never authored.** It is a function of the JSON and is rewritten wholesale
  on each save; it is never edited directly and carries no independent truth. Storing it (rather than
  computing it per query) keeps it indexable by SQLite and keeps read paths simple.

- **Cleanup output is wrapped to JSON at the boundary.** The AI still returns plain/markdown text; the
  `SessionCleanupRunner` converts it to canonical JSON via `MarkdownToProseMirror`
  ([RICH-002](../../docs/issues/RICH-002-markdown-to-prosemirror-module.md)) before `CompleteCleanup`,
  so the content columns stay always-JSON and the Cleaned editor renders formatting. The structured
  `{cleanedMarkdown, …}` Cleanup contract is formalized later in
  [RICH-004](../../docs/issues/RICH-004-cleanup-structured-output-contract.md); this slice only needs the
  columns to be JSON.

- **No backfill.** There is no production data (PRD-0006), so the database and all prior EF migrations
  are dropped and a single fresh `InitialCreate` is generated in the new shape. This establishes the
  schema baseline that later slices add onto.

## Invariants preserved

- **Append-only Revisions** ([ADR-0003](0003-append-only-content-revisions.md)) are unchanged: a Raw or
  Cleaned Revision now snapshots the canonical JSON, and **Stale**/timestamp derivation is untouched.
- **Raw is human-owned and never mutated by the server** (CONTEXT.md) — still true; only the
  representation changed.
- **The editor stays uncontrolled with the keyed-remount pattern** (FE-014/FE-015): seeded once from
  server JSON, debounce-saving JSON, never re-fed from props into a live editor.

## Considered options

- **Keep plain text, add a parallel markdown string** — no new module surface, but markdown can't carry
  inline mention atoms cleanly and re-parsing markdown on every render is lossy and ambiguous. Rejected:
  doesn't support the mention/metadata direction PRD-0006 requires.
- **Store only JSON and project to plaintext at query time** — avoids the two extra columns, but pushes
  JSON parsing into every list/search query and forfeits a SQLite-indexable text column. Rejected for
  read-path complexity and search cost.
- **Store only the derived plaintext and re-derive JSON** — impossible: plaintext is lossy, formatting
  and mentions cannot be reconstructed. Rejected outright.

## Consequences

- Two derived columns (`RawPlainText`, `CleanedPlainText`) must be kept in lockstep with the JSON — the
  aggregate is the single writer, recomputing them at every save point, so they can never drift.
- Consumers split cleanly by intent: the editor and Revision drill-down read JSON; preview, search, and
  AI input read plaintext. Adding a consumer means choosing one column, not parsing JSON ad hoc.
- The pure `ProseMirrorToPlainText` and `MarkdownToProseMirror` modules are now load-bearing on the write
  path, not just the editor — their correctness is covered by their own unit suites plus aggregate-level
  projection tests.
- **Forward references:** the `mention` node carries `{personId, label}` and will project to metadata in
  [RICH-005](../../docs/issues/RICH-005-person-aggregate-directory-endpoints.md)+
  ([RICH-006](../../docs/issues/RICH-006-person-resolver-mention-projection.md)); Person becomes its own
  aggregate there. Those slices build on this baseline without changing the content representation.
