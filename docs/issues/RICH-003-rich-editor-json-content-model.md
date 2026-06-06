# RICH-003 — Rich editor + JSON Content model (keystone)

**Phase:** 13 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0006 · **Introduces:** ADR-0009

## What to build

The keystone of PRD-0006: make **Raw** and **Cleaned** **Content** a canonical ProseMirror/tiptap
JSON document instead of plain text, with a Notion-style editor. Everything rich hangs off this slice.

End-to-end behavior:

- **Canonical representation** is ProseMirror/tiptap JSON (small node/mark set) stored in the existing
  content columns, for both **Raw** and **Cleaned**. A **derived plaintext projection**
  (via `ProseMirrorToPlainText`, RICH-001) is produced on every save and is what the search index
  consumes — formatting never hides content from search.
- **Editor UI:** tiptap replaces both `<textarea>`s. **Raw** and **Cleaned** switch from side-by-side
  to a **toggle** (one view at a time). **Cleaned** shows a "run **Cleanup** to generate" empty state
  before any **Cleanup** has run. The editor stays uncontrolled and keeps the keyed-remount pattern
  (keyed on **Session** identity / latest **Cleaned** **Revision**, seeded from server JSON,
  debounce-saving JSON) — server state is never fed back into a live editor (avoids cursor-reset bugs).
- **Raw stays simple by default** — plain typing produces a plain document; formatting is opt-in.
- **Revisions** snapshot the canonical JSON (ADR-0003 still append-only; **Stale**/timestamp derivation
  unchanged). The Revision drill-down renders JSON **read-only** with formatting, not `<pre>` text.
- **Migration:** there is **no production data** — drop the database and existing EF migrations and
  generate **one fresh initial migration** in the new shape. No backfill. This establishes the schema
  baseline that later slices (RICH-005/010/011) add onto.

Write **ADR-0009** for the content-model decision (ProseMirror JSON canonical + derived plaintext;
mention-projected metadata and Person-as-aggregate are noted as forward references to RICH-005+).
Match the format of `docs/adr/0008-compound-form-components.md`. The AI **Cleanup** output contract
change is **out of scope here** — it lands in RICH-004; this slice only needs Cleaned to be a
hand-editable rich editor.

## Acceptance criteria

- [ ] Raw and Cleaned Content persist as canonical ProseMirror/tiptap JSON in the existing columns; a
      derived plaintext projection is written on every save and drives the search index.
- [ ] Both editors are tiptap; Raw/Cleaned is a toggle (one at a time); Cleaned shows the
      "run Cleanup to generate" empty state before any Cleanup has run.
- [ ] Formatting (headings, lists, emphasis) round-trips across save and reload in both editors.
- [ ] Editing Cleaned by hand saves and snapshots a Revision carrying the formatted JSON; the Revision
      drill-down renders past versions read-only with their formatting.
- [ ] Search still finds entries by their words (index built from derived plaintext of current state).
- [ ] The database + EF migrations are dropped and a single fresh initial migration is generated; the
      app boots clean (Aspire 5247/7247/4247) with no backfill.
- [ ] ADR-0009 is written in the ADR format.
- [ ] Ships with unit + integration + functional coverage (functional covers the tiptap write +
      toggle + reload flow per ADR-0006).

## Blocked by

- RICH-001
