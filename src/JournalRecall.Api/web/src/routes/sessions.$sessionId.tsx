import { useEffect, useRef, useState } from 'react'
import { createFileRoute } from '@tanstack/react-router'
import { useRevision, useRevisions, useSaveDraft, useSession } from '@/features/sessions/useSessions'
import { Button } from '@/shared/ui/button'

export const Route = createFileRoute('/sessions/$sessionId')({
  component: SessionEditor,
})

function SessionEditor() {
  const { sessionId } = Route.useParams()
  const { data: session, isLoading, isError } = useSession(sessionId)
  const saveDraft = useSaveDraft(sessionId)

  const [text, setText] = useState('')
  const [hydrated, setHydrated] = useState(false)
  const [viewing, setViewing] = useState<number | null>(null) // a past Revision number, or null
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
    <section className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold text-content">Session</h1>
        <SaveStatus pending={saveDraft.isPending} success={saveDraft.isSuccess} error={saveDraft.isError} />
      </div>

      <textarea
        value={text}
        onChange={(event) => onChange(event.target.value)}
        placeholder="Write freely…"
        autoFocus
        className="min-h-[50vh] w-full resize-none rounded-lg border border-border bg-surface-2 p-4 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
      />

      <RevisionHistory sessionId={sessionId} viewing={viewing} onView={setViewing} />
    </section>
  )
}

function RevisionHistory({
  sessionId,
  viewing,
  onView,
}: {
  sessionId: string
  viewing: number | null
  onView: (revisionNumber: number | null) => void
}) {
  const { data: revisions } = useRevisions(sessionId)
  const { data: revision } = useRevision(sessionId, viewing)

  if (!revisions?.length) return null

  return (
    <div className="space-y-2 border-t border-border pt-4">
      <h2 className="text-sm font-medium text-muted">Raw history</h2>
      <ul className="flex flex-wrap gap-2">
        {revisions.map((r) => (
          <li key={r.revisionNumber}>
            <Button
              variant={viewing === r.revisionNumber ? 'primary' : 'ghost'}
              onPress={() => onView(viewing === r.revisionNumber ? null : r.revisionNumber)}
            >
              v{r.revisionNumber} · {new Date(r.createdAt).toLocaleString()}
            </Button>
          </li>
        ))}
      </ul>

      {viewing != null && revision ? (
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <span className="text-sm text-muted">Viewing version {revision.revisionNumber} (read-only)</span>
            <Button onPress={() => onView(null)}>Back to editing</Button>
          </div>
          <pre className="max-h-[40vh] overflow-auto whitespace-pre-wrap rounded-lg border border-border bg-surface-3 p-4 text-content">
            {revision.content}
          </pre>
        </div>
      ) : null}
    </div>
  )
}

function SaveStatus({ pending, success, error }: { pending: boolean; success: boolean; error: boolean }) {
  const label = error ? 'Save failed' : pending ? 'Saving…' : success ? 'Saved' : 'Autosaves as you write'
  return <span className="text-sm text-muted">{label}</span>
}
