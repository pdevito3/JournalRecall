import { type Editor, useEditorState } from '@tiptap/react'
import { HugeiconsIcon, type IconSvgElement } from '@hugeicons/react'
import {
  CheckListIcon,
  CodeIcon,
  Heading01Icon,
  Heading02Icon,
  Heading03Icon,
  HighlighterIcon,
  LeftToRightListBulletIcon,
  LeftToRightListNumberIcon,
  Link01Icon,
  MinusSignIcon,
  QuoteDownIcon,
  SourceCodeIcon,
  TextBoldIcon,
  TextItalicIcon,
  TextStrikethroughIcon,
  TextUnderlineIcon,
} from '@hugeicons/core-free-icons'
import { Button } from '@/shared/ui/button'
import { cn } from '@/shared/utils/cn'

/**
 * The formatting toolbar for the editable journaling surfaces (RICH-013, ADR-0010). Presentational over a
 * single `useEditor` instance: each button issues a tiptap command and reflects active state via
 * `editor.isActive(...)` + `aria-pressed`. Active state is derived through `useEditorState`, which
 * subscribes to editor transactions so the toolbar re-renders (and `aria-pressed` updates) as the selection
 * and stored marks change. Rendered only when the editor is `editable`, so it never appears on the read-only
 * Revision drill-down. The exposed node/mark set is exactly the markdown-expressible set.
 */
export function EditorToolbar({ editor }: { editor: Editor }) {
  const active = useEditorState({
    editor,
    selector: ({ editor }) => ({
      h1: editor.isActive('heading', { level: 1 }),
      h2: editor.isActive('heading', { level: 2 }),
      h3: editor.isActive('heading', { level: 3 }),
      bold: editor.isActive('bold'),
      italic: editor.isActive('italic'),
      strike: editor.isActive('strike'),
      underline: editor.isActive('underline'),
      highlight: editor.isActive('highlight'),
      code: editor.isActive('code'),
      link: editor.isActive('link'),
      bulletList: editor.isActive('bulletList'),
      orderedList: editor.isActive('orderedList'),
      taskList: editor.isActive('taskList'),
      blockquote: editor.isActive('blockquote'),
      codeBlock: editor.isActive('codeBlock'),
    }),
  })

  return (
    <div
      role="toolbar"
      aria-label="Formatting"
      data-testid="editor-toolbar"
      className="flex flex-wrap items-center gap-0.5 rounded-lg border border-border bg-surface-2 p-1"
    >
      <ToolbarButton
        icon={Heading01Icon}
        label="Heading 1"
        active={active.h1}
        onPress={() => editor.chain().focus().toggleHeading({ level: 1 }).run()}
      />
      <ToolbarButton
        icon={Heading02Icon}
        label="Heading 2"
        active={active.h2}
        onPress={() => editor.chain().focus().toggleHeading({ level: 2 }).run()}
      />
      <ToolbarButton
        icon={Heading03Icon}
        label="Heading 3"
        active={active.h3}
        onPress={() => editor.chain().focus().toggleHeading({ level: 3 }).run()}
      />

      <Separator />

      <ToolbarButton
        icon={TextBoldIcon}
        label="Bold"
        active={active.bold}
        onPress={() => editor.chain().focus().toggleBold().run()}
      />
      <ToolbarButton
        icon={TextItalicIcon}
        label="Italic"
        active={active.italic}
        onPress={() => editor.chain().focus().toggleItalic().run()}
      />
      <ToolbarButton
        icon={TextStrikethroughIcon}
        label="Strikethrough"
        active={active.strike}
        onPress={() => editor.chain().focus().toggleStrike().run()}
      />
      <ToolbarButton
        icon={TextUnderlineIcon}
        label="Underline"
        active={active.underline}
        onPress={() => editor.chain().focus().toggleUnderline().run()}
      />
      <ToolbarButton
        icon={HighlighterIcon}
        label="Highlight"
        active={active.highlight}
        onPress={() => editor.chain().focus().toggleHighlight().run()}
      />
      <ToolbarButton
        icon={CodeIcon}
        label="Inline code"
        active={active.code}
        onPress={() => editor.chain().focus().toggleCode().run()}
      />
      <ToolbarButton
        icon={Link01Icon}
        label="Link"
        active={active.link}
        onPress={() => setLink(editor)}
      />

      <Separator />

      <ToolbarButton
        icon={LeftToRightListBulletIcon}
        label="Bullet list"
        active={active.bulletList}
        onPress={() => editor.chain().focus().toggleBulletList().run()}
      />
      <ToolbarButton
        icon={LeftToRightListNumberIcon}
        label="Ordered list"
        active={active.orderedList}
        onPress={() => editor.chain().focus().toggleOrderedList().run()}
      />
      <ToolbarButton
        icon={CheckListIcon}
        label="Task list"
        active={active.taskList}
        onPress={() => editor.chain().focus().toggleTaskList().run()}
      />
      <ToolbarButton
        icon={QuoteDownIcon}
        label="Blockquote"
        active={active.blockquote}
        onPress={() => editor.chain().focus().toggleBlockquote().run()}
      />
      <ToolbarButton
        icon={SourceCodeIcon}
        label="Code block"
        active={active.codeBlock}
        onPress={() => editor.chain().focus().toggleCodeBlock().run()}
      />

      <Separator />

      <ToolbarButton
        icon={MinusSignIcon}
        label="Horizontal rule"
        onPress={() => editor.chain().focus().setHorizontalRule().run()}
      />
    </div>
  )
}

/**
 * Minimal v1 link entry (RICH-013): a `window.prompt` for the URL. A non-empty URL applies a `link` mark over
 * the current selection/word; an empty/cancelled prompt clears an active link. Upgradeable to a popover later.
 */
function setLink(editor: Editor) {
  const previous = (editor.getAttributes('link').href as string | undefined) ?? ''
  const url = window.prompt('Link URL', previous)
  // Cancelled → leave as-is.
  if (url === null) return
  // Empty → clear any link on the current selection.
  if (url.trim().length === 0) {
    editor.chain().focus().extendMarkRange('link').unsetLink().run()
    return
  }
  editor.chain().focus().extendMarkRange('link').setLink({ href: url.trim() }).run()
}

function Separator() {
  return <span aria-hidden className="mx-0.5 h-5 w-px bg-border" />
}

function ToolbarButton({
  icon,
  label,
  active = false,
  onPress,
}: {
  icon: IconSvgElement
  label: string
  active?: boolean
  onPress: () => void
}) {
  return (
    <Button
      variant="icon"
      aria-label={label}
      aria-pressed={active}
      onPress={onPress}
      className={cn(active && 'bg-surface-3 text-content')}
    >
      <HugeiconsIcon icon={icon} size={18} />
    </Button>
  )
}
