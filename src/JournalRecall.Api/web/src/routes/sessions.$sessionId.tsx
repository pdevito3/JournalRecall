import { useEffect, useRef, useState } from 'react'
import { createFileRoute } from '@tanstack/react-router'
import { useSaveDraft, useSession } from '@/features/sessions/useSessions'

export const Route = createFileRoute('/sessions/$sessionId')({
  component: SessionEditor,
})

function SessionEditor() {
  const { sessionId } = Route.useParams()
  const { data: session, isLoading, isError } = useSession(sessionId)
  const saveDraft = useSaveDraft(sessionId)

  const [text, setText] = useState('')
  const [hydrated, setHydrated] = useState(false)
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  // Hydrate once from the server, then local state owns the text (survives reloads).
  useEffect(() => {
    if (session && !hydrated) {
      setText(session.rawDraft)
      setHydrated(true)
    }
  }, [session, hydrated])

  useEffect(() => () => clearTimeout(timer.current), [])

  function onChange(value: string) {
    setText(value)
    clearTimeout(timer.current)
    timer.current = setTimeout(() => saveDraft.mutate(value), 600) // debounced autosave
  }

  if (isLoading) return <p className="text-muted">Loading…</p>
  if (isError) return <p className="text-muted">This session could not be found.</p>

  return (
    <section className="space-y-3">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold text-content">Session</h1>
        <SaveStatus pending={saveDraft.isPending} success={saveDraft.isSuccess} error={saveDraft.isError} />
      </div>
      <textarea
        value={text}
        onChange={(event) => onChange(event.target.value)}
        placeholder="Write freely…"
        autoFocus
        className="min-h-[60vh] w-full resize-none rounded-lg border border-border bg-surface-2 p-4 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
      />
    </section>
  )
}

function SaveStatus({ pending, success, error }: { pending: boolean; success: boolean; error: boolean }) {
  const label = error ? 'Save failed' : pending ? 'Saving…' : success ? 'Saved' : 'Autosaves as you write'
  return <span className="text-sm text-muted">{label}</span>
}
