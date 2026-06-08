import { describe, expect, it } from 'vitest'
import { render, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { Session } from '@/features/sessions/api'
import { cleanedEditorKey } from './session-editor'
import { RichEditor } from './rich-editor'

// A minimal valid Session DTO; tests override only the Cleaned-relevant fields.
function makeSession(over: Partial<Session> = {}): Session {
  return {
    id: 's1',
    createdAt: '2026-01-01T00:00:00Z',
    rawDraft: '',
    cleanedDraft: '',
    synopsis: '',
    cleanupStatus: 'Clean',
    cleanedHasHandEdits: false,
    cleanedRegenerationRevisionNumber: 1,
    topics: [],
    people: [],
    moods: [],
    activity: 'None',
    suggestions: [],
    peopleProposals: [],
    location: null,
    ...over,
  }
}

const doc = (text: string) =>
  JSON.stringify({ type: 'doc', content: [{ type: 'paragraph', content: [{ type: 'text', text }] }] })

describe('cleanedEditorKey', () => {
  it('is stable when only cleanedDraft changes (a user hand-edit autosave)', () => {
    // Issue 0028: a hand-edit save mutates cleanedDraft but must NOT change the key — keying on the
    // draft would remount the editor on every autosave, resetting the caret and dropping keystrokes.
    const before = makeSession({ cleanedDraft: doc('typed one') })
    const afterSave = makeSession({ cleanedDraft: doc('typed one two') })
    expect(cleanedEditorKey(afterSave)).toBe(cleanedEditorKey(before))
  })

  it('changes when a server regeneration advances the token (Cleanup re-run / approved People-tag)', () => {
    const before = makeSession({ cleanedRegenerationRevisionNumber: 1, cleanedDraft: doc('v1') })
    const regenerated = makeSession({ cleanedRegenerationRevisionNumber: 2, cleanedDraft: doc('v2 fresh') })
    expect(cleanedEditorKey(regenerated)).not.toBe(cleanedEditorKey(before))
  })

  it('is scoped per Session', () => {
    expect(cleanedEditorKey(makeSession({ id: 'a' }))).not.toBe(cleanedEditorKey(makeSession({ id: 'b' })))
  })
})

// The Cleaned editor is uncontrolled and keyed on cleanedEditorKey(session). React preserves a mounted
// component across re-renders while its key is unchanged, and remounts it when the key changes. We assert
// that contract directly against the same key the SessionEditor uses, via the identity of the rendered
// .ProseMirror node (a remount replaces it).
describe('Cleaned editor remount on autosave vs regeneration', () => {
  it('does NOT remount when a hand-edit autosave changes cleanedDraft (caret/keystrokes preserved)', async () => {
    let session = makeSession({ cleanedRegenerationRevisionNumber: 1, cleanedDraft: doc('hello') })
    const { rerender } = render(
      <RichEditor key={cleanedEditorKey(session)} initialContent={session.cleanedDraft} onChange={() => {}} autoFocus />,
    )
    const pm = await waitFor(() => {
      const el = document.querySelector('.ProseMirror') as HTMLElement | null
      expect(el).not.toBeNull()
      return el!
    })

    // Type, then simulate the autosave refetch landing: cleanedDraft advances to the just-saved text. The
    // key is recomputed from the same session — it must not change, so the editor stays mounted.
    const editable = document.querySelector('.ProseMirror') as HTMLElement
    await userEvent.click(editable)
    await userEvent.keyboard(' world')

    session = makeSession({ cleanedRegenerationRevisionNumber: 1, cleanedDraft: doc('hello world') })
    rerender(
      <RichEditor key={cleanedEditorKey(session)} initialContent={session.cleanedDraft} onChange={() => {}} autoFocus />,
    )

    // Same DOM node ⇒ no remount ⇒ caret + in-flight keystrokes survive.
    expect(document.querySelector('.ProseMirror')).toBe(pm)
  })

  it('remounts and re-seeds when a server regeneration advances the token', async () => {
    let session = makeSession({ cleanedRegenerationRevisionNumber: 1, cleanedDraft: doc('v1 ai copy') })
    const { rerender } = render(
      <RichEditor key={cleanedEditorKey(session)} initialContent={session.cleanedDraft} onChange={() => {}} />,
    )
    const pm = await waitFor(() => {
      const el = document.querySelector('.ProseMirror') as HTMLElement | null
      expect(el).not.toBeNull()
      return el!
    })

    // A Cleanup re-run (or approved People-tag) bumps the regeneration token and the draft together.
    session = makeSession({ cleanedRegenerationRevisionNumber: 2, cleanedDraft: doc('v2 regenerated') })
    rerender(<RichEditor key={cleanedEditorKey(session)} initialContent={session.cleanedDraft} onChange={() => {}} />)

    // New DOM node ⇒ remount ⇒ re-seeded with the regenerated content.
    await waitFor(() => expect(document.querySelector('.ProseMirror')).not.toBe(pm))
    await waitFor(() => expect(document.body.textContent).toContain('v2 regenerated'))
  })
})
