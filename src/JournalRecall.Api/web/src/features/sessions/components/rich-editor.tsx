import { useEffect, useRef } from 'react'
import { EditorContent, useEditor, type Extensions, type JSONContent } from '@tiptap/react'
import StarterKit from '@tiptap/starter-kit'
import { Placeholder } from '@tiptap/extensions'
import { cn } from '@/shared/utils/cn'

// Canonical node/mark set for RICH-003 (LOCKED wire contract):
//   nodes: doc, paragraph, heading (1,2,3), bulletList, orderedList, listItem, blockquote, codeBlock
//   marks: bold, italic, code
// Everything outside this set is disabled. StarterKit also bundles utility extensions
// (text, hardBreak, dropcursor, gapcursor, listKeymap, trailingNode, undoRedo) which are kept —
// they carry no out-of-set nodes/marks and just make editing pleasant.
//
// Mention is intentionally NOT here (arrives in RICH-007). To extend later, append a mention
// extension to `extensions` below — nothing else needs to change.
function buildExtensions(placeholder?: string): Extensions {
  return [
    StarterKit.configure({
      heading: { levels: [1, 2, 3] },
      // Out-of-set marks/nodes — explicitly disabled.
      strike: false,
      horizontalRule: false,
      link: false,
      underline: false,
    }),
    Placeholder.configure({ placeholder: placeholder ?? '' }),
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
}

/**
 * Notion-style tiptap editor over the canonical ProseMirror JSON content model.
 *
 * Uncontrolled by design (keyed-remount pattern, FE-013/FE-015): content is seeded from
 * `initialContent` on mount and never re-fed from props — server state is not pushed back into a
 * live editor, which avoids cursor-reset bugs. To re-seed from the server, remount with a new key.
 *
 * StarterKit's markdown input rules supply the formatting affordances (`# ` → heading, `- ` → bullet,
 * `> ` → quote, ``` → code block, `**bold**`, `*italic*`, `` `code` ``), so no toolbar is required.
 */
export function RichEditor({
  initialContent,
  onChange,
  placeholder,
  editable = true,
  autoFocus = false,
  className,
}: RichEditorProps) {
  // Keep the latest onChange without re-creating the editor (which would lose editor state).
  const onChangeRef = useRef(onChange)
  onChangeRef.current = onChange

  const editor = useEditor({
    extensions: buildExtensions(placeholder),
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
  )
}
