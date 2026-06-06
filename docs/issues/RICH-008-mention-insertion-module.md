# RICH-008 — `MentionInsertion` deep module

**Phase:** 13 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0006

## What to build

The pure ProseMirror transform that inserts approved People tags into the **Cleaned** document
**without a second AI pass**. Given a document plus a set of approved spans (with their resolved
`PersonId` + `label`), wrap each span in a `mention` node. This is what makes AI-proposed tags
(RICH-009) trustworthy: approval inserts deterministically exactly where proposed.

Pure and deterministic — operates on the document + spans, no DB, no AI. Must handle multiple spans in
one document with correct offset bookkeeping (inserting one mention shifts later offsets) and leave
non-span text untouched.

## Acceptance criteria

- [ ] A pure transform wraps each approved span in a `mention` node `{ personId, label }`, preserving
      surrounding text and marks.
- [ ] Multiple spans in one document are all inserted correctly (offsets stay consistent across
      insertions).
- [ ] Spans that no longer match (stale offsets / changed text) are skipped without corrupting the
      document.
- [ ] Unit tests cover single/multiple insertions, offset correctness, and the skip-on-mismatch case.

## Blocked by

- RICH-006
