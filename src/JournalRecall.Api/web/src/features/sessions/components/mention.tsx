import { forwardRef, useEffect, useImperativeHandle, useMemo, useRef, useState } from 'react'
import Mention from '@tiptap/extension-mention'
import {
  NodeViewWrapper,
  ReactNodeViewRenderer,
  ReactRenderer,
  type NodeViewProps,
} from '@tiptap/react'
import type { SuggestionProps, SuggestionKeyDownProps } from '@tiptap/suggestion'
import type { Person } from '@/features/sessions/api'
import { useCreatePerson, usePeople } from '@/features/sessions/useSessions'

/**
 * The host wiring an @-mention editor needs (RICH-007). Both are read on demand so the once-built editor
 * always sees the latest directory and a stable create path, without recreating the editor on each render.
 */
export interface MentionConfig {
  /** The latest Person directory snapshot, read fresh on each `@` query. */
  getPeople: () => Person[]
  /** Inline-create a directory Person from the `@` "create new" path. */
  createPerson: (label: string) => Promise<Person>
}

/** What a picked/created suggestion inserts: the durable link + a display snapshot. */
interface PickedPerson {
  personId: string
  label: string
}

/**
 * A stable <see cref="MentionConfig"/> for the editing surfaces: the once-built editor reads the latest
 * directory and create path through refs, so it never needs recreating when the directory refreshes.
 */
export function useMentionConfig(): MentionConfig {
  const { data: people } = usePeople()
  const create = useCreatePerson()
  const peopleRef = useRef<Person[]>([])
  peopleRef.current = people ?? []
  const createRef = useRef(create)
  createRef.current = create
  return useMemo<MentionConfig>(
    () => ({
      getPeople: () => peopleRef.current,
      createPerson: (label) => createRef.current.mutateAsync(label),
    }),
    [],
  )
}

/**
 * Renders a mention as the directory's *live* label (falling back to the stored snapshot), so renaming a
 * Person propagates to every existing mention site without rewriting stored content. Reads the directory
 * from the React Query cache, so it re-renders when the directory refreshes.
 */
function MentionNodeView({ node }: NodeViewProps) {
  const { data: people } = usePeople()
  const attrs = node.attrs as { personId: string | null; label: string | null }
  const snapshot = attrs.label ?? ''
  const live = people?.find((p) => p.id === attrs.personId)?.label
  return (
    <NodeViewWrapper
      as="span"
      className="mention rounded bg-accent/15 px-1 font-medium text-accent"
      data-mention
    >
      @{live ?? snapshot}
    </NodeViewWrapper>
  )
}

interface MentionListHandle {
  onKeyDown: (props: SuggestionKeyDownProps) => boolean
}

interface MentionListProps extends SuggestionProps<Person, PickedPerson> {
  config: MentionConfig
}

/** The `@` autocomplete popup: matching directory People plus an inline "create new" row. */
const MentionList = forwardRef<MentionListHandle, MentionListProps>((props, ref) => {
  const [index, setIndex] = useState(0)
  const query = props.query.trim()
  const hasExact = props.items.some((p) => p.label.toLowerCase() === query.toLowerCase())
  const showCreate = query.length > 0 && !hasExact
  const count = props.items.length + (showCreate ? 1 : 0)

  useEffect(() => setIndex(0), [props.items, props.query])

  const pick = async (i: number) => {
    if (showCreate && i === props.items.length) {
      const created = await props.config.createPerson(query)
      props.command({ personId: created.id, label: created.label })
      return
    }
    const person = props.items[i]
    if (person) props.command({ personId: person.id, label: person.label })
  }

  useImperativeHandle(ref, () => ({
    onKeyDown: ({ event }) => {
      if (count === 0) return false
      if (event.key === 'ArrowDown') {
        setIndex((i) => (i + 1) % count)
        return true
      }
      if (event.key === 'ArrowUp') {
        setIndex((i) => (i - 1 + count) % count)
        return true
      }
      if (event.key === 'Enter') {
        void pick(index)
        return true
      }
      return false
    },
  }))

  return (
    <div className="mention-list min-w-40 overflow-hidden rounded-lg border border-border bg-surface-2 py-1 text-sm shadow-lg">
      {props.items.map((person, i) => (
        <button
          key={person.id}
          type="button"
          aria-selected={i === index}
          onMouseDown={(e) => {
            e.preventDefault()
            void pick(i)
          }}
          className={`block w-full px-3 py-1 text-left ${i === index ? 'bg-surface-3 text-content' : 'text-muted'}`}
        >
          @{person.label}
        </button>
      ))}
      {showCreate ? (
        <button
          type="button"
          aria-selected={index === props.items.length}
          onMouseDown={(e) => {
            e.preventDefault()
            void pick(props.items.length)
          }}
          className={`block w-full px-3 py-1 text-left ${
            index === props.items.length ? 'bg-surface-3 text-content' : 'text-accent'
          }`}
        >
          Create “{query}”
        </button>
      ) : null}
      {count === 0 ? <div className="px-3 py-1 text-muted">No people yet — type a name</div> : null}
    </div>
  )
})
MentionList.displayName = 'MentionList'

/** Pins the popup just below the caret. */
function position(popup: HTMLElement, clientRect?: (() => DOMRect | null) | null) {
  const rect = clientRect?.()
  if (!rect) return
  popup.style.position = 'fixed'
  popup.style.left = `${rect.left}px`
  popup.style.top = `${rect.bottom + 4}px`
  popup.style.zIndex = '50'
}

/** A `@` suggestion that never fires — used for read-only views, which still need the node in the schema. */
const inertSuggestion = { char: '@', items: () => [], render: () => ({}) }

/**
 * The @-mention extension (RICH-007): a `mention` node carrying `{ personId, label }`, rendered as the
 * live directory label, with `@` autocomplete over the User's directory and inline create. The node name
 * stays `mention` to match the server contract (ProseMirrorToPlainText / MentionProjection read it).
 *
 * The node + live-label NodeView are always registered so any view (including read-only revision history)
 * preserves and displays stored mentions; the `@` autocomplete is wired only when a <see cref="config"/>
 * is supplied (the editing surfaces).
 */
export function createPersonMention(config?: MentionConfig) {
  const base = Mention.extend({
    name: 'mention',
    addAttributes() {
      return {
        personId: { default: null },
        label: { default: null },
      }
    },
    addNodeView() {
      return ReactNodeViewRenderer(MentionNodeView)
    },
  })

  if (!config) return base.configure({ suggestion: inertSuggestion })

  return base.configure({
    suggestion: {
      char: '@',
      items: ({ query }) => {
        const q = query.trim().toLowerCase()
        return config
          .getPeople()
          .filter((p) => p.label.toLowerCase().includes(q))
          .slice(0, 6)
      },
      command: ({ editor, range, props }) => {
        const picked = props as unknown as PickedPerson
        editor
          .chain()
          .focus()
          .insertContentAt(range, [
            { type: 'mention', attrs: { personId: picked.personId, label: picked.label } },
            { type: 'text', text: ' ' },
          ])
          .run()
      },
      render: () => {
        let renderer: ReactRenderer<MentionListHandle, MentionListProps> | null = null
        let popup: HTMLElement | null = null

        return {
          onStart: (props) => {
            renderer = new ReactRenderer(MentionList, {
              props: { ...props, config },
              editor: props.editor,
            })
            popup = document.createElement('div')
            document.body.appendChild(popup)
            popup.appendChild(renderer.element)
            position(popup, props.clientRect)
          },
          onUpdate: (props) => {
            renderer?.updateProps({ ...props, config })
            if (popup) position(popup, props.clientRect)
          },
          onKeyDown: (props) => {
            if (props.event.key === 'Escape') {
              popup?.remove()
              return true
            }
            return renderer?.ref?.onKeyDown(props) ?? false
          },
          onExit: () => {
            popup?.remove()
            popup = null
            renderer?.destroy()
            renderer = null
          },
        }
      },
    },
  })
}
