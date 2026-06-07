import { useEffect, useRef } from 'react'
import { EditorContent, useEditor, type Extensions, type JSONContent } from '@tiptap/react'
import StarterKit from '@tiptap/starter-kit'
import { Placeholder } from '@tiptap/extensions'
import { Highlight } from '@tiptap/extension-highlight'
import { TaskItem, TaskList } from '@tiptap/extension-list'
import { cn } from '@/shared/utils/cn'
import { createPersonMention, type MentionConfig } from './mention'
import { EditorToolbar } from './editor-toolbar'

// Markdown-expressible node/mark set (ADR-0010 / RICH-013) — exactly what MarkdownToProseMirror can emit:
//   nodes: doc, paragraph, heading (1,2,3), bulletList, orderedList, listItem, taskList, taskItem,
//          blockquote, codeBlock, horizontalRule, mention (RICH-007, the one sanctioned non-markdown atom)
//   marks: bold, italic, code, strike, underline, highlight (single-color), link
// StarterKit also bundles utility extensions (text, hardBreak, dropcursor, gapcursor, listKeymap,
// trailingNode, undoRedo) which are kept — they carry no out-of-set nodes/marks and just make editing
// pleasant. Each extension brings its markdown input rules, kept alongside the formatting toolbar.
//
// The `mention` node is always registered (so read-only views preserve stored mentions); the `@`
// autocomplete is wired only when a `mention` config is supplied (the editing surfaces).
function buildExtensions(placeholder?: string, mention?: MentionConfig): Extensions {
  return [
    StarterKit.configure({
      heading: { levels: [1, 2, 3] },
      // strike / underline / horizontalRule / link ship in StarterKit and are now in-set — leave enabled.
      // Links don't navigate on click inside the editor; href/target/rel stay at tiptap defaults so they
      // match what the server converter (RICH-012) emits.
      link: { openOnClick: false },
    }),
    // Single-color highlight (no `multicolor`): `==text==` carries no color attribute (ADR-0010).
    Highlight,
    // Task lists / todos. TaskItem honors the editor's `editable` state for its checkbox automatically,
    // so the read-only Revision view renders non-interactive checkboxes.
    TaskList,
    TaskItem.configure({ nested: true }),
    Placeholder.configure({ placeholder: placeholder ?? '' }),
    createPersonMention(mention),
  ]
}

/** An empty tiptap document — the canonical representation of never-written content. */
const EMPTY_DOC: JSONContent = { type: 'doc', content: [{ type: 'paragraph' }] }

/**
 * Parse a server-stored JSON string into tiptap content. Treats ""/null/whitespace and any
 * unparseable or non-object payload as an empty document so the editor never crashes on bad input
 * (LOCKED contract: "" / null / invalid-JSON → empty doc).
 */
export function parseContent(initialContent: string | null | undefined): JSONContent {
  if (!initialContent || initialContent.trim().length === 0) return EMPTY_DOC
  try {
    const parsed = JSON.parse(initialContent)
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) return parsed as JSONContent
    return EMPTY_DOC
  } catch {
    return EMPTY_DOC
  }
}

export interface RichEditorProps {
  /** Server-stored JSON string. ""/null/invalid → empty document. Read once on mount (uncontrolled). */
  initialContent: string
  /** Receives the serialized tiptap JSON on every edit. Parent owns debounce/save. */
  onChange?: (json: string) => void
  placeholder?: string
  /** Default true. false renders read-only (revision drill-down). */
  editable?: boolean
  autoFocus?: boolean
  className?: string
  /** Wires the `@`-mention autocomplete (editing surfaces); omit for read-only views. */
  mention?: MentionConfig
}

/**
 * Notion-style tiptap editor over the canonical ProseMirror JSON content model.
 *
 * Uncontrolled by design (keyed-remount pattern, FE-013/FE-015): content is seeded from
 * `initialContent` on mount and never re-fed from props — server state is not pushed back into a
 * live editor, which avoids cursor-reset bugs. To re-seed from the server, remount with a new key.
 *
 * A formatting toolbar (RICH-013) renders above the content on the editable surfaces (Raw + Cleaned) and
 * never on the read-only Revision drill-down. Markdown input rules from the extensions still work alongside
 * it (`# ` → heading, `- ` → bullet, `- [ ] ` → task, `> ` → quote, `---` → rule, `**bold**`, `~~strike~~`,
 * `==highlight==`, `++underline++`, `` `code` ``).
 */
export function RichEditor({
  initialContent,
  onChange,
  placeholder,
  editable = true,
  autoFocus = false,
  className,
  mention,
}: RichEditorProps) {
  // Keep the latest onChange without re-creating the editor (which would lose editor state).
  const onChangeRef = useRef(onChange)
  onChangeRef.current = onChange

  const editor = useEditor({
    extensions: buildExtensions(placeholder, mention),
    content: parseContent(initialContent),
    editable,
    autofocus: autoFocus ? 'end' : false,
    onUpdate: ({ editor }) => {
      onChangeRef.current?.(JSON.stringify(editor.getJSON()))
    },
  })

  // Toggle read-only without remounting (e.g. if `editable` flips).
  useEffect(() => {
    editor?.setEditable(editable)
  }, [editor, editable])

  return (
    <div className="space-y-2">
      {/* Toolbar only on the editable surfaces (Raw + Cleaned), never the read-only Revision drill-down. */}
      {editable && editor ? <EditorToolbar editor={editor} /> : null}
      <EditorContent
        editor={editor}
        className={cn(
          'rich-editor w-full rounded-lg border border-border p-4 text-content',
          editable
            ? 'min-h-[50vh] bg-surface-2 focus-within:ring-2 focus-within:ring-accent'
            : 'max-h-[40vh] overflow-auto bg-surface-3',
          className,
        )}
      />
    </div>
  )
}
