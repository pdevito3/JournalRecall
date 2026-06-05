import { type ReactNode, useEffect, useRef, useState } from 'react'
import { createFileRoute } from '@tanstack/react-router'
import type { CleanupStatus, Session } from '@/features/sessions/api'
import {
  useCleanup,
  useRevision,
  useRevisions,
  useSaveDraft,
  useSession,
} from '@/features/sessions/useSessions'
import { Button } from '@/shared/ui/button'
import { cn } from '@/shared/utils/cn'

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
  if (isError || !session) return <p className="text-muted">This session could not be found.</p>

  const hasCleaned = session.cleanedDraft.length > 0

  return (
    <section className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold text-content">Session</h1>
        <SaveStatus pending={saveDraft.isPending} success={saveDraft.isSuccess} error={saveDraft.isError} />
      </div>

      <CleanupBar session={session} />

      {/* Raw and Cleaned side by side once a Cleaned copy exists; Raw alone until then. */}
      <div className={cn('grid gap-4', hasCleaned && 'lg:grid-cols-2')}>
        <div className="space-y-2">
          {hasCleaned ? <PanelLabel>Raw (yours)</PanelLabel> : null}
          <textarea
            value={text}
            onChange={(event) => onChange(event.target.value)}
            placeholder="Write freely…"
            autoFocus
            className="min-h-[50vh] w-full resize-none rounded-lg border border-border bg-surface-2 p-4 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
          />
        </div>

        {hasCleaned ? (
          <div className="space-y-2">
            <PanelLabel>Cleaned (AI)</PanelLabel>
            <pre className="min-h-[50vh] w-full overflow-auto whitespace-pre-wrap rounded-lg border border-border bg-surface-3 p-4 text-content">
              {session.cleanedDraft}
            </pre>
          </div>
        ) : null}
      </div>

      {session.synopsis ? (
        <div className="space-y-1 rounded-lg border border-border bg-surface-2 p-4">
          <PanelLabel>Synopsis</PanelLabel>
          <p className="text-content">{session.synopsis}</p>
        </div>
      ) : null}

      <RevisionHistory sessionId={sessionId} viewing={viewing} onView={setViewing} />
    </section>
  )
}

function PanelLabel({ children }: { children: ReactNode }) {
  return <h2 className="text-sm font-medium text-muted">{children}</h2>
}

const STATUS_LABELS: Record<CleanupStatus, string> = {
  NotRun: 'Not cleaned',
  Running: 'Cleaning…',
  Clean: 'Clean',
  Stale: 'Stale — Raw changed since cleanup',
  Failed: 'Cleanup failed',
}

function CleanupBar({ session }: { session: Session }) {
  const { run, running, progress, error } = useCleanup(session.id)
  // Server status, or the live in-flight status while a run streams.
  const status: CleanupStatus = running ? 'Running' : session.cleanupStatus
  const isStale = status === 'Stale'

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-border bg-surface-2 p-3">
      <div className="flex items-center gap-3">
        <span
          className={cn(
            'inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium',
            isStale || status === 'Failed'
              ? 'bg-amber-500/15 text-amber-400'
              : status === 'Clean'
                ? 'bg-accent/15 text-accent'
                : 'bg-surface-3 text-muted',
          )}
        >
          {STATUS_LABELS[status]}
        </span>
        {running && progress.length > 0 ? (
          <span className="text-sm text-muted">{progress[progress.length - 1]}</span>
        ) : null}
        {error ? <span className="text-sm text-amber-400">Something went wrong.</span> : null}
      </div>

      <Button variant="primary" onPress={() => void run()} isDisabled={running}>
        {running ? 'Cleaning…' : isStale ? 'Re-run cleanup' : 'Clean up with AI'}
      </Button>
    </div>
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
