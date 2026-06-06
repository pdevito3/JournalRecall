import { type ReactNode, useEffect, useRef, useState } from 'react'
import { createFileRoute } from '@tanstack/react-router'
import { useForm } from '@tanstack/react-form'
import { z } from 'zod'
import { FormShell, TextField, SelectField, applyServerErrors } from '@/shared/forms'
import {
  cleanedRevisionsQueryOptions,
  revisionsQueryOptions,
  sessionQueryOptions,
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
  KNOWN_MOODS,
  type CleanupStatus,
  type RevisionSummary,
  type Session,
  type Suggestion,
} from '@/features/sessions'
import { Button } from '@/shared/ui/button'
import { cn } from '@/shared/utils/cn'

export const Route = createFileRoute('/sessions/$sessionId')({
  // Await the primary Session (blocks first paint); let the Revision streams prefetch in the
  // background so the editor renders without waiting on history. Components keep reading via useQuery.
  loader: async ({ context: { queryClient }, params: { sessionId } }) => {
    await queryClient.ensureQueryData(sessionQueryOptions(sessionId))
    void queryClient.prefetchQuery(revisionsQueryOptions(sessionId))
    void queryClient.prefetchQuery(cleanedRevisionsQueryOptions(sessionId))
  },
  component: SessionEditorRoute,
})

// Remount the editor per Session: the Router reuses this component across param changes, so a `key`
// on Session identity guarantees a fresh editor (and lets local state seed directly from the server).
function SessionEditorRoute() {
  const { sessionId } = Route.useParams()
  return <SessionEditor key={sessionId} sessionId={sessionId} />
}

function SessionEditor({ sessionId }: { sessionId: string }) {
  const { data: session } = useSession(sessionId)
  const saveDraft = useSaveDraft(sessionId)

  // Fresh per Session (keyed remount), so seed Raw Draft directly from server data — no hydration latch.
  const [text, setText] = useState(session.rawDraft)
  const [viewingRaw, setViewingRaw] = useState<number | null>(null)
  const [viewingCleaned, setViewingCleaned] = useState<number | null>(null)
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  useEffect(() => () => clearTimeout(timer.current), [])

  function onChange(value: string) {
    setText(value)
    clearTimeout(timer.current)
    timer.current = setTimeout(() => saveDraft.mutate(value), 600) // debounced autosave
  }

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

        {hasCleaned ? <CleanedEditorBoundary session={session} /> : null}
      </div>

      {session.synopsis ? (
        <div className="space-y-1 rounded-lg border border-border bg-surface-2 p-4">
          <PanelLabel>Synopsis</PanelLabel>
          <p className="text-content">{session.synopsis}</p>
        </div>
      ) : null}

      <SuggestionChips session={session} />

      <MetadataEditor key={metadataKey(session)} session={session} />

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

const CUSTOM_MOOD = 'Custom'

const moodOptions = [
  { id: '', label: '— none —' },
  ...KNOWN_MOODS.map((m) => ({ id: m, label: m })),
  { id: CUSTOM_MOOD, label: 'Custom…' },
]

/** Validation-only: the conditional "custom value required when Mood is Custom" rule. */
export const metadataSchema = z
  .object({
    topics: z.string(),
    people: z.string(),
    moodKey: z.string(),
    customMood: z.string(),
  })
  .superRefine((value, ctx) => {
    if (value.moodKey === CUSTOM_MOOD && value.customMood.trim().length === 0) {
      ctx.addIssue({ code: 'custom', message: 'Enter a custom mood.', path: ['customMood'] })
    }
  })

type MetadataFormValues = z.infer<typeof metadataSchema>

// Change-token for the metadata editor: the Session DTO carries no revision field, so derive one from
// the metadata values themselves. When the server changes them (accepted Suggestion, Cleanup re-run),
// this key changes and the editor remounts, re-seeding its defaults from the fresh server values.
function metadataKey(session: Session): string {
  return JSON.stringify([session.topics, session.people, session.mood])
}

/** Per-Session manual metadata: Topics, People, and a Mood (known or Custom free text). */
function MetadataEditor({ session }: { session: Session }) {
  const save = useSaveMetadata(session.id)

  const form = useForm({
    defaultValues: {
      topics: session.topics.join(', '),
      people: session.people.join(', '),
      moodKey: session.mood?.key ?? '',
      customMood: session.mood?.customValue ?? '',
    } satisfies MetadataFormValues,
    validators: { onBlur: metadataSchema },
    onSubmit: async ({ value }) => {
      const mood =
        value.moodKey === ''
          ? null
          : value.moodKey === CUSTOM_MOOD
            ? { key: CUSTOM_MOOD, customValue: value.customMood.trim() }
            : { key: value.moodKey, customValue: null }
      try {
        await save.mutateAsync({ topics: splitList(value.topics), people: splitList(value.people), mood })
      } catch (error) {
        applyServerErrors(form, error)
      }
    },
  })

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <PanelLabel>Metadata</PanelLabel>
        <SaveStatus pending={save.isPending} success={save.isSuccess} error={save.isError} />
      </div>
      <FormShell
        form={form}
        submitLabel="Save metadata"
        className="space-y-3 rounded-lg border border-border bg-surface-2 p-4"
      >
        <div className="grid gap-3 sm:grid-cols-2">
          <form.Field name="topics">
            {(field) => <TextField field={field} label="Topics (comma-separated)" placeholder="work, parenthood" />}
          </form.Field>
          <form.Field name="people">
            {(field) => <TextField field={field} label="People (comma-separated)" placeholder="Sam, Alex" />}
          </form.Field>
        </div>
        <form.Field name="moodKey">
          {(field) => <SelectField field={field} label="Mood" options={moodOptions} />}
        </form.Field>
        <form.Subscribe selector={(s) => s.values.moodKey}>
          {(moodKey) =>
            moodKey === CUSTOM_MOOD ? (
              <form.Field name="customMood">
                {(field) => <TextField field={field} label="Custom mood" placeholder="bittersweet" />}
              </form.Field>
            ) : null
          }
        </form.Subscribe>
      </FormShell>
    </div>
  )
}

function splitList(value: string): string[] {
  return value
    .split(',')
    .map((t) => t.trim())
    .filter((t) => t.length > 0)
}

// Change-token for the Cleaned editor: the latest cleaned Revision number. A Cleanup re-run appends a
// new cleaned Revision (server regeneration), so the number changes → the editor remounts → re-seeds
// from the fresh server copy. Within a stable number, local unsaved edits persist (no remount on
// keystrokes/refetch). Concurrent-edit policy: local edits win until a save point; a server
// regeneration re-seeds. Falls back to the seed text so the first paint (before history loads) is keyed
// stably rather than flapping when the revisions list arrives.
function CleanedEditorBoundary({ session }: { session: Session }) {
  const { data: revisions } = useCleanedRevisions(session.id)
  const latest = revisions?.at(-1)
  const token = latest ? `v${latest.revisionNumber}` : session.cleanedDraft
  return <CleanedEditor key={`${session.id}:${token}`} session={session} />
}

/** The AI-derived Cleaned copy, hand-editable. Edits debounce-save and append a Cleaned Revision. */
function CleanedEditor({ session }: { session: Session }) {
  const saveCleaned = useSaveCleaned(session.id)
  // Fresh per cleaned-Revision (keyed remount), so seed the Cleaned copy directly from server data.
  const [text, setText] = useState(session.cleanedDraft)
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  useEffect(() => () => clearTimeout(timer.current), [])

  function onChange(value: string) {
    setText(value)
    clearTimeout(timer.current)
    timer.current = setTimeout(() => saveCleaned.mutate(value), 600) // debounced autosave
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
