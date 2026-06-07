# RICH-013 — Formatting toolbar + expanded editor extensions

**Phase:** 14 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0006 · **Governed by:** ADR-0010

## What to build

Give the journaling editor a full **formatting toolbar** and expand its node/mark set to the
markdown-expressible set defined in [ADR-0010](../adr/0010-markdown-expressible-content-model.md),
replacing the input-rules-only ("no toolbar required") affordance from RICH-003. Slash-command-style
formatting is abandoned (poorly supported in tiptap); a visible toolbar is the affordance.

End-to-end behavior:

- **Editor schema expansion** (tiptap v3): re-enable StarterKit's `strike`, `underline`,
  `horizontalRule`, and `link` (disabled by RICH-003); add `@tiptap/extension-highlight`
  (single-color) and `@tiptap/extension-list` (`taskList`/`taskItem`). **Text alignment is intentionally
  not added** — it has no markdown form (ADR-0010). Markdown input rules from these extensions are kept
  alongside the toolbar.
- **Toolbar UI** renders whenever the editor is `editable` — i.e. the **Raw** editor and the **Cleaned**
  hand-edit editor, with identical buttons; **never** on the read-only Revision drill-down. Buttons:
  headings (1–3), bold, italic, strike, underline, highlight, inline code, link, bullet/ordered/task
  lists, blockquote, code block, horizontal rule. Buttons use **hugeicons** (add
  `@hugeicons/react` + its free-icon set) and the existing `Button` `icon` variant, with active state
  driven by `editor.isActive(...)` and `aria-pressed`.
- **Link** entry uses a minimal URL prompt for v1 (upgradeable to a popover later).
- **Task items** are interactive checkboxes when editable; non-interactive in read-only views.
- Read-only Revision views render all new nodes/marks (highlight, strike, underline, task lists, HR,
  links) from stored JSON with their formatting.

No EF/schema change — content stays JSON. Independent of RICH-012: the editor may hold a node before the
AI converter can emit it; the parity invariant is fully satisfied once both slices land.

## Acceptance criteria

- [ ] Strike, underline, horizontalRule, and link are re-enabled; highlight (single-color) and
      taskList/taskItem are added; text-align is not present.
- [ ] A formatting toolbar (hugeicons) appears on both editable surfaces (Raw + Cleaned) and not on the
      read-only Revision drill-down; each button toggles its node/mark and reflects active state.
- [ ] All new formatting round-trips across debounce-save and reload in both editable editors.
- [ ] Read-only Revision drill-down renders the new nodes/marks with formatting; task checkboxes are
      non-interactive there.
- [ ] The editor stays uncontrolled with the keyed-remount pattern; markdown input rules still work.
- [ ] Ships with functional coverage for the toolbar + reload round-trip (per ADR-0006), driving the
      editor via `.rich-editor .ProseMirror`.

## Blocked by

- None - can start immediately
