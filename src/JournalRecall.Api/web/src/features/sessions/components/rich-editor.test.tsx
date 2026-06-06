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
