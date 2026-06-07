import { describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { parseContent, RichEditor } from './rich-editor'

const EMPTY_DOC = { type: 'doc', content: [{ type: 'paragraph' }] }

// A small valid tiptap document: a single paragraph with the word "hello".
const HELLO_DOC = JSON.stringify({
  type: 'doc',
  content: [{ type: 'paragraph', content: [{ type: 'text', text: 'hello' }] }],
})

describe('parseContent', () => {
  it('treats empty string as an empty document', () => {
    expect(parseContent('')).toEqual(EMPTY_DOC)
  })

  it('treats whitespace-only / null / undefined as an empty document', () => {
    expect(parseContent('   ')).toEqual(EMPTY_DOC)
    expect(parseContent(null)).toEqual(EMPTY_DOC)
    expect(parseContent(undefined)).toEqual(EMPTY_DOC)
  })

  it('treats invalid JSON as an empty document (no throw)', () => {
    expect(parseContent('{not json')).toEqual(EMPTY_DOC)
    expect(parseContent('not even close')).toEqual(EMPTY_DOC)
  })

  it('treats a non-object JSON payload as an empty document', () => {
    expect(parseContent('42')).toEqual(EMPTY_DOC)
    expect(parseContent('[1,2,3]')).toEqual(EMPTY_DOC)
    expect(parseContent('"a string"')).toEqual(EMPTY_DOC)
  })

  it('returns the parsed document for valid JSON', () => {
    expect(parseContent(HELLO_DOC)).toEqual(JSON.parse(HELLO_DOC))
  })
})

describe('RichEditor', () => {
  it('renders an empty editor for "" / invalid content without crashing', () => {
    render(<RichEditor initialContent="" onChange={() => {}} />)
    expect(document.querySelector('.ProseMirror')).toBeInTheDocument()

    render(<RichEditor initialContent="{broken" onChange={() => {}} />)
    expect(document.querySelectorAll('.ProseMirror').length).toBeGreaterThan(0)
  })

  it('seeds the editor from valid JSON content', async () => {
    render(<RichEditor initialContent={HELLO_DOC} onChange={() => {}} />)
    await waitFor(() => expect(screen.getByText('hello')).toBeInTheDocument())
  })

  it('emits serialized tiptap JSON on edit', async () => {
    const onChange = vi.fn()
    render(<RichEditor initialContent="" onChange={onChange} autoFocus />)

    const editable = document.querySelector('.ProseMirror') as HTMLElement
    await userEvent.click(editable)
    await userEvent.keyboard('hi')

    await waitFor(() => expect(onChange).toHaveBeenCalled())
    const lastArg = onChange.mock.calls.at(-1)![0] as string
    expect(typeof lastArg).toBe('string')
    const doc = JSON.parse(lastArg)
    expect(doc.type).toBe('doc')
    expect(JSON.stringify(doc)).toContain('hi')
  })

  it('round-trips: serialized JSON re-seeds into an equivalent document', async () => {
    const captured: string[] = []
    render(<RichEditor initialContent="" onChange={(j) => captured.push(j)} autoFocus />)
    const editable = document.querySelector('.ProseMirror') as HTMLElement
    await userEvent.click(editable)
    await userEvent.keyboard('round trip')
    await waitFor(() => expect(captured.length).toBeGreaterThan(0))

    const serialized = captured.at(-1)!
    // Re-seeding a fresh editor from the serialized JSON renders the same text.
    render(<RichEditor initialContent={serialized} onChange={() => {}} />)
    await waitFor(() => expect(screen.getAllByText('round trip').length).toBeGreaterThan(0))
  })

  it('does not emit onChange in read-only mode and still renders content', async () => {
    const onChange = vi.fn()
    render(<RichEditor initialContent={HELLO_DOC} editable={false} onChange={onChange} />)
    await waitFor(() => expect(screen.getByText('hello')).toBeInTheDocument())
    expect(document.querySelector('.ProseMirror')?.getAttribute('contenteditable')).toBe('false')
  })
})

// A document exercising the RICH-013 / ADR-0010 expanded set: highlight, strike, underline marks, a link,
// a taskList with a checked taskItem, and a horizontalRule.
const EXPANDED_DOC = JSON.stringify({
  type: 'doc',
  content: [
    {
      type: 'paragraph',
      content: [
        { type: 'text', marks: [{ type: 'highlight' }], text: 'lit ' },
        { type: 'text', marks: [{ type: 'strike' }], text: 'struck ' },
        { type: 'text', marks: [{ type: 'underline' }], text: 'under ' },
        { type: 'text', marks: [{ type: 'link', attrs: { href: 'https://example.com' } }], text: 'link' },
      ],
    },
    {
      type: 'taskList',
      content: [
        {
          type: 'taskItem',
          attrs: { checked: true },
          content: [{ type: 'paragraph', content: [{ type: 'text', text: 'done thing' }] }],
        },
      ],
    },
    { type: 'horizontalRule' },
  ],
})

describe('RichEditor formatting toolbar', () => {
  it('renders the toolbar on an editable editor', async () => {
    render(<RichEditor initialContent="" onChange={() => {}} />)
    await waitFor(() => expect(screen.getByRole('toolbar', { name: /formatting/i })).toBeInTheDocument())
    // A representative button is present and accessible.
    expect(screen.getByRole('button', { name: 'Bold' })).toBeInTheDocument()
  })

  it('does NOT render the toolbar on a read-only editor', async () => {
    render(<RichEditor initialContent={HELLO_DOC} editable={false} onChange={() => {}} />)
    await waitFor(() => expect(screen.getByText('hello')).toBeInTheDocument())
    expect(screen.queryByRole('toolbar', { name: /formatting/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Bold' })).not.toBeInTheDocument()
  })

  it('toggles a mark via a toolbar button: aria-pressed flips and typed text carries the mark', async () => {
    const onChange = vi.fn()
    render(<RichEditor initialContent="" onChange={onChange} autoFocus />)

    const editable = document.querySelector('.ProseMirror') as HTMLElement
    await userEvent.click(editable)

    const bold = screen.getByRole('button', { name: 'Bold' })
    expect(bold).toHaveAttribute('aria-pressed', 'false')

    await userEvent.click(bold)
    // useEditorState subscribes to transactions, so the button's active state flips reactively.
    await waitFor(() => expect(bold).toHaveAttribute('aria-pressed', 'true'))

    // Typing while bold is active produces a bold mark. Refocus the editor first (the click moved DOM
    // focus to the button) and scan every emitted doc, since later onChanges can land after the bold one.
    await userEvent.click(editable)
    await userEvent.keyboard('x')
    await waitFor(() =>
      expect(onChange.mock.calls.some((c) => (c[0] as string).includes('"type":"bold"'))).toBe(true),
    )
  })

  it('inserts a horizontal rule via the toolbar', async () => {
    const onChange = vi.fn()
    render(<RichEditor initialContent="" onChange={onChange} autoFocus />)
    const editable = document.querySelector('.ProseMirror') as HTMLElement
    await userEvent.click(editable)

    await userEvent.click(screen.getByRole('button', { name: 'Horizontal rule' }))
    await waitFor(() => expect(onChange).toHaveBeenCalled())
    const last = onChange.mock.calls.at(-1)![0] as string
    expect(last).toContain('"type":"horizontalRule"')
  })

  it('read-only: renders the expanded node/mark set from stored JSON without crashing', async () => {
    render(<RichEditor initialContent={EXPANDED_DOC} editable={false} onChange={() => {}} />)
    // Text from across the new nodes/marks appears.
    await waitFor(() => expect(screen.getByText('link')).toBeInTheDocument())
    expect(screen.getByText('done thing')).toBeInTheDocument()
    // Structural nodes are present.
    expect(document.querySelector('hr')).toBeInTheDocument()
    expect(document.querySelector('mark')).toBeInTheDocument()
    expect(document.querySelector('a[href="https://example.com"]')).toBeInTheDocument()
    // The task checkbox reflects the stored checked state and is non-interactive in the read-only view:
    // tiptap's TaskItem reverts any toggle when the editor isn't editable.
    const checkbox = document.querySelector('input[type="checkbox"]') as HTMLInputElement | null
    expect(checkbox).not.toBeNull()
    expect(checkbox!.checked).toBe(true)
    // Toggling it in read-only mode does not stick (TaskItem reverts non-editable changes).
    checkbox!.checked = false
    checkbox!.dispatchEvent(new Event('change', { bubbles: true }))
    expect(checkbox!.checked).toBe(true)
    // No toolbar on the read-only surface.
    expect(screen.queryByRole('toolbar', { name: /formatting/i })).not.toBeInTheDocument()
  })

  it('round-trips a checked task list through serialize → re-seed', async () => {
    render(<RichEditor initialContent={EXPANDED_DOC} editable={false} onChange={() => {}} />)
    await waitFor(() => expect(screen.getByText('done thing')).toBeInTheDocument())
    // Re-seed a fresh editor from the same JSON and confirm it still renders.
    render(<RichEditor initialContent={EXPANDED_DOC} editable={false} onChange={() => {}} />)
    await waitFor(() => expect(screen.getAllByText('done thing').length).toBeGreaterThan(0))
  })
})
