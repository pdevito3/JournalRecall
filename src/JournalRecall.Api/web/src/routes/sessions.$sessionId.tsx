import { type ReactNode, useEffect, useRef, useState } from 'react'
import { createFileRoute } from '@tanstack/react-router'
import { KNOWN_MOODS, type CleanupStatus, type RevisionSummary, type Session } from '@/features/sessions/api'
import type { Suggestion } from '@/features/sessions/api'
import {
  useCleanedRevision,
  useCleanedRevisions,
  useCleanup,
  useRespondToSuggestion,
  useRevision,
  useRevisions,
  useSaveCleaned,
  useSaveDraft,
  useSaveMetadata,
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
  const [viewingRaw, setViewingRaw] = useState<number | null>(null)
  const [viewingCleaned, setViewingCleaned] = useState<number | null>(null)
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
        <div className="space-y-0.5">
          <h1 className="text-lg font-semibold text-content">Session</h1>
          {session.location ? (
            <p className="text-xs text-muted">
              📍 {session.location.latitude.toFixed(5)}, {session.location.longitude.toFixed(5)}
            </p>
          ) : null}
        </div>
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

        {hasCleaned ? <CleanedEditor session={session} /> : null}
      </div>

      {session.synopsis ? (
        <div className="space-y-1 rounded-lg border border-border bg-surface-2 p-4">
          <PanelLabel>Synopsis</PanelLabel>
          <p className="text-content">{session.synopsis}</p>
        </div>
      ) : null}

      <SuggestionChips session={session} />

      <MetadataEditor session={session} />

      <RawHistory sessionId={sessionId} viewing={viewingRaw} onView={setViewingRaw} />
      <CleanedHistory sessionId={sessionId} viewing={viewingCleaned} onView={setViewingCleaned} />
    </section>
  )
}

function PanelLabel({ children }: { children: ReactNode }) {
  return <h2 className="text-sm font-medium text-muted">{children}</h2>
}

/** AI metadata Suggestions from the last Cleanup, each accept/reject-able (issue 0012). */
function SuggestionChips({ session }: { session: Session }) {
  const respond = useRespondToSuggestion(session.id)
  if (session.suggestions.length === 0) return null

  function label(s: Suggestion): string {
    const prefix = s.kind === 'Topic' ? '#' : s.kind === 'Person' ? '@' : ''
    return s.kind === 'Mood' ? `mood: ${s.moodCustomValue ?? s.value}` : `${prefix}${s.value}`
  }

  return (
    <div className="space-y-2 rounded-lg border border-dashed border-border bg-surface-2 p-4">
      <PanelLabel>AI suggestions</PanelLabel>
      <ul className="flex flex-wrap gap-2">
        {session.suggestions.map((s) => (
          <li
            key={`${s.kind}:${s.value}`}
            className="flex items-center gap-1 rounded-full border border-border bg-surface-3 py-0.5 pl-3 pr-1 text-sm text-content"
          >
            <span>{label(s)}</span>
            <button
              type="button"
              aria-label={`Accept ${label(s)}`}
              className="ml-1 rounded-full px-1.5 text-accent hover:bg-accent/15"
              onClick={() => respond.mutate({ suggestion: s, action: 'accept' })}
            >
              ✓
            </button>
            <button
              type="button"
              aria-label={`Reject ${label(s)}`}
              className="rounded-full px-1.5 text-muted hover:bg-surface-2 hover:text-content"
              onClick={() => respond.mutate({ suggestion: s, action: 'reject' })}
            >
              ✕
            </button>
          </li>
        ))}
      </ul>
    </div>
  )
}

const NO_MOOD = ''
const CUSTOM_MOOD = 'Custom'

/** Per-Session manual metadata: Topics, People, and a Mood (known or Custom free text). */
function MetadataEditor({ session }: { session: Session }) {
  const save = useSaveMetadata(session.id)
  const [topics, setTopics] = useState(session.topics.join(', '))
  const [people, setPeople] = useState(session.people.join(', '))
  const [moodKey, setMoodKey] = useState(session.mood?.key ?? NO_MOOD)
  const [customMood, setCustomMood] = useState(session.mood?.customValue ?? '')

  function onSave() {
    const mood =
      moodKey === NO_MOOD
        ? null
        : moodKey === CUSTOM_MOOD
          ? { key: CUSTOM_MOOD, customValue: customMood.trim() }
          : { key: moodKey, customValue: null }
    save.mutate({ topics: splitList(topics), people: splitList(people), mood })
  }

  return (
    <div className="space-y-3 rounded-lg border border-border bg-surface-2 p-4">
      <div className="flex items-center justify-between">
        <PanelLabel>Metadata</PanelLabel>
        <SaveStatus pending={save.isPending} success={save.isSuccess} error={save.isError} />
      </div>
      <div className="grid gap-3 sm:grid-cols-2">
        <label className="space-y-1">
          <span className="text-sm text-muted">Topics (comma-separated)</span>
          <input
            value={topics}
            onChange={(e) => setTopics(e.target.value)}
            placeholder="work, parenthood"
            className="w-full rounded-lg border border-border bg-surface-3 px-3 py-2 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
          />
        </label>
        <label className="space-y-1">
          <span className="text-sm text-muted">People (comma-separated)</span>
          <input
            value={people}
            onChange={(e) => setPeople(e.target.value)}
            placeholder="Sam, Alex"
            className="w-full rounded-lg border border-border bg-surface-3 px-3 py-2 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
          />
        </label>
      </div>
      <div className="flex flex-wrap items-end gap-3">
        <label className="space-y-1">
          <span className="text-sm text-muted">Mood</span>
          <select
            value={moodKey}
            onChange={(e) => setMoodKey(e.target.value)}
            className="block rounded-lg border border-border bg-surface-3 px-3 py-2 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
          >
            <option value={NO_MOOD}>— none —</option>
            {KNOWN_MOODS.map((m) => (
              <option key={m} value={m}>
                {m}
              </option>
            ))}
            <option value={CUSTOM_MOOD}>Custom…</option>
          </select>
        </label>
        {moodKey === CUSTOM_MOOD ? (
          <label className="space-y-1">
            <span className="text-sm text-muted">Custom mood</span>
            <input
              value={customMood}
              onChange={(e) => setCustomMood(e.target.value)}
              placeholder="bittersweet"
              className="block rounded-lg border border-border bg-surface-3 px-3 py-2 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
            />
          </label>
        ) : null}
        <Button
          variant="primary"
          onPress={onSave}
          isDisabled={save.isPending || (moodKey === CUSTOM_MOOD && customMood.trim().length === 0)}
        >
          Save metadata
        </Button>
      </div>
    </div>
  )
}

function splitList(value: string): string[] {
  return value
    .split(',')
    .map((t) => t.trim())
    .filter((t) => t.length > 0)
}

/** The AI-derived Cleaned copy, hand-editable. Edits debounce-save and append a Cleaned Revision. */
function CleanedEditor({ session }: { session: Session }) {
  const saveCleaned = useSaveCleaned(session.id)
  const [text, setText] = useState(session.cleanedDraft)
  const serverRef = useRef(session.cleanedDraft)
  const dirtyRef = useRef(false)
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  // Adopt server-originated changes (e.g. a re-run regenerated the copy), but never clobber a local
  // edit that hasn't been saved yet.
  useEffect(() => {
    if (!dirtyRef.current && session.cleanedDraft !== serverRef.current) {
      serverRef.current = session.cleanedDraft
      setText(session.cleanedDraft)
    }
  }, [session.cleanedDraft])

  useEffect(() => () => clearTimeout(timer.current), [])

  function onChange(value: string) {
    setText(value)
    dirtyRef.current = true
    clearTimeout(timer.current)
    timer.current = setTimeout(() => {
      saveCleaned.mutate(value, {
        onSuccess: () => {
          dirtyRef.current = false
          serverRef.current = value
        },
      })
    }, 600)
  }

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between">
        <PanelLabel>Cleaned (AI · editable)</PanelLabel>
        <SaveStatus pending={saveCleaned.isPending} success={saveCleaned.isSuccess} error={saveCleaned.isError} />
      </div>
      <textarea
        value={text}
        onChange={(event) => onChange(event.target.value)}
        className="min-h-[50vh] w-full resize-none rounded-lg border border-border bg-surface-3 p-4 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
      />
    </div>
  )
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

  function handleRun() {
    // Warn before a re-run overwrites hand-edits — the prior version is kept in history regardless.
    if (session.cleanedHasHandEdits) {
      const ok = window.confirm(
        'Re-running cleanup will overwrite your hand-edited Cleaned copy. The current version is kept in history. Continue?',
      )
      if (!ok) return
    }
    void run()
  }

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
        {session.cleanedHasHandEdits ? <span className="text-xs text-muted">hand-edited</span> : null}
        {running && progress.length > 0 ? (
          <span className="text-sm text-muted">{progress[progress.length - 1]}</span>
        ) : null}
        {error ? <span className="text-sm text-amber-400">Something went wrong.</span> : null}
      </div>

      <Button variant="primary" onPress={handleRun} isDisabled={running}>
        {running ? 'Cleaning…' : isStale || status === 'Clean' || status === 'Failed' ? 'Re-run cleanup' : 'Clean up with AI'}
      </Button>
    </div>
  )
}

function RawHistory({
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
  return (
    <RevisionDrilldown title="Raw history" revisions={revisions} viewing={viewing} onView={onView} content={revision?.content} />
  )
}

function CleanedHistory({
  sessionId,
  viewing,
  onView,
}: {
  sessionId: string
  viewing: number | null
  onView: (revisionNumber: number | null) => void
}) {
  const { data: revisions } = useCleanedRevisions(sessionId)
  const { data: revision } = useCleanedRevision(sessionId, viewing)
  return (
    <RevisionDrilldown
      title="Cleaned history"
      revisions={revisions}
      viewing={viewing}
      onView={onView}
      content={revision?.content}
    />
  )
}

/** Presentational Revision history: a row of versions and a read-only view of the selected one. */
function RevisionDrilldown({
  title,
  revisions,
  viewing,
  onView,
  content,
}: {
  title: string
  revisions: RevisionSummary[] | undefined
  viewing: number | null
  onView: (revisionNumber: number | null) => void
  content: string | undefined
}) {
  if (!revisions?.length) return null

  return (
    <div className="space-y-2 border-t border-border pt-4">
      <h2 className="text-sm font-medium text-muted">{title}</h2>
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

      {viewing != null && content != null ? (
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <span className="text-sm text-muted">Viewing version {viewing} (read-only)</span>
            <Button onPress={() => onView(null)}>Back</Button>
          </div>
          <pre className="max-h-[40vh] overflow-auto whitespace-pre-wrap rounded-lg border border-border bg-surface-3 p-4 text-content">
            {content}
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
