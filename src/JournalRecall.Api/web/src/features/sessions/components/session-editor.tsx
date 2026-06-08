import { type ReactNode, useState } from 'react'
import { useForm } from '@tanstack/react-form'
import { z } from 'zod'
import {
  useCleanedRevision,
  useCleanedRevisions,
  useCleanup,
  usePeople,
  useRespondToPersonProposal,
  useRespondToSuggestion,
  useRevision,
  useRevisions,
  useSaveCleaned,
  useSaveDraft,
  useSaveMetadata,
  useSession,
  useTopics,
} from '@/features/sessions/useSessions'
import {
  ACTIVITY_ICONS,
  KNOWN_ACTIVITIES,
  KNOWN_MOODS,
  type CleanupStatus,
  type PersonTagProposal,
  type RevisionSummary,
  type Session,
  type Suggestion,
} from '@/features/sessions/api'
import { Form, createForm } from '@/shared/forms'
import { Button } from '@/shared/ui/button'
import { cn } from '@/shared/utils/cn'
import { useAutosave } from '@/shared/utils/use-autosave'
import { RichEditor } from './rich-editor'
import { useMentionConfig } from './mention'

type EditorTab = 'raw' | 'cleaned'

export function SessionEditor({ sessionId }: { sessionId: string }) {
  const { data: session } = useSession(sessionId)
  const saveDraft = useSaveDraft(sessionId)
  const mention = useMentionConfig()

  const [viewingRaw, setViewingRaw] = useState<number | null>(null)
  const [viewingCleaned, setViewingCleaned] = useState<number | null>(null)
  // Raw and Cleaned share one pane via a toggle; Raw is the default view.
  const [tab, setTab] = useState<EditorTab>('raw')

  // Debounced autosave of the serialized tiptap JSON.
  const onChange = useAutosave(saveDraft.mutate)

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

      {/* One view at a time: a Raw/Cleaned toggle replaces the old side-by-side grid. */}
      <div className="space-y-2">
        <EditorTabs tab={tab} onTab={setTab} />
        {tab === 'raw' ? (
          // Uncontrolled, keyed per Session at the route level → seed Raw directly from server JSON.
          <RichEditor
            initialContent={session.rawDraft}
            onChange={onChange}
            placeholder="Write freely… type @ to tag someone"
            autoFocus
            mention={mention}
          />
        ) : hasCleaned ? (
          // Keyed on a server-*regeneration* token — the Cleaned Revision number of the last Cleanup run
          // or approved People-tag insertion — NOT on `cleanedDraft`. A hand-edit autosave appends a
          // Cleaned Revision and refetches `cleanedDraft` to the just-saved text; keying on the draft (or
          // the raw revision count) would remount the uncontrolled editor on every save, resetting the
          // caret and dropping keystrokes typed during the debounce + round-trip (issue 0028). The token
          // doesn't move on a hand-edit, so the editor stays mounted; it only changes on a regeneration,
          // which re-seeds it. Concurrent-edit policy: local unsaved edits win until a save point — only a
          // server regeneration re-seeds (FE-015). Both the token and the seed ride the same Session DTO,
          // so the remount and the re-seed stay in lockstep.
          <CleanedEditor key={cleanedEditorKey(session)} session={session} />
        ) : (
          <CleanedEmptyState />
        )}
      </div>

      {session.synopsis ? (
        <div className="space-y-1 rounded-lg border border-border bg-surface-2 p-4">
          <PanelLabel>Synopsis</PanelLabel>
          <p className="text-content">{session.synopsis}</p>
        </div>
      ) : null}

      <SuggestionChips session={session} />

      <PersonProposals session={session} />

      <MetadataEditor key={metadataKey(session)} session={session} />

      <RawHistory sessionId={sessionId} viewing={viewingRaw} onView={setViewingRaw} />
      <CleanedHistory sessionId={sessionId} viewing={viewingCleaned} onView={setViewingCleaned} />
    </section>
  )
}

function PanelLabel({ children }: { children: ReactNode }) {
  return <h2 className="text-sm font-medium text-muted">{children}</h2>
}

/** Segmented Raw/Cleaned toggle — one editor is shown at a time. */
function EditorTabs({ tab, onTab }: { tab: EditorTab; onTab: (tab: EditorTab) => void }) {
  return (
    <div role="tablist" aria-label="Editor view" className="inline-flex rounded-lg border border-border bg-surface-2 p-0.5">
      {(
        [
          ['raw', 'Raw (yours)'],
          ['cleaned', 'Cleaned (AI · editable)'],
        ] as const
      ).map(([value, label]) => (
        <button
          key={value}
          type="button"
          role="tab"
          aria-selected={tab === value}
          onClick={() => onTab(value)}
          className={cn(
            'rounded-md px-3 py-1 text-sm font-medium transition-colors',
            tab === value ? 'bg-surface-3 text-content' : 'text-muted hover:text-content',
          )}
        >
          {label}
        </button>
      ))}
    </div>
  )
}

/** Cleaned tab before any Cleanup has produced a copy. */
function CleanedEmptyState() {
  return (
    <div className="flex min-h-[50vh] flex-col items-center justify-center gap-2 rounded-lg border border-dashed border-border bg-surface-2 p-4 text-center">
      <p className="text-sm font-medium text-content">No cleaned copy yet</p>
      <p className="text-sm text-muted">Run Cleanup to generate a polished version of your entry.</p>
    </div>
  )
}

/** AI metadata Suggestions from the last Cleanup, each accept/reject-able (issue 0012). */
function SuggestionChips({ session }: { session: Session }) {
  const respond = useRespondToSuggestion(session.id)
  if (session.suggestions.length === 0) return null

  function label(s: Suggestion): string {
    return s.kind === 'Mood' ? `mood: ${s.value}` : `#${s.value}`
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

/**
 * AI People-tag proposals from the last Cleanup (PRD-0006, RICH-009): one review card per proposed Person,
 * showing every sentence the tag would land in. Each is approved as a whole (binding to the matched Person,
 * reassigned to a different existing Person, or created new) or rejected — the AI never writes to the People
 * directory or the prose without this approval. Approval inserts mentions deterministically.
 */
function PersonProposals({ session }: { session: Session }) {
  if (session.peopleProposals.length === 0) return null

  return (
    <div className="space-y-3 rounded-lg border border-dashed border-border bg-surface-2 p-4" data-testid="people-proposals">
      <PanelLabel>People the AI suggests tagging</PanelLabel>
      <ul className="space-y-3">
        {session.peopleProposals.map((p) => (
          <ProposalCard key={p.label} session={session} proposal={p} />
        ))}
      </ul>
    </div>
  )
}

function ProposalCard({ session, proposal }: { session: Session; proposal: PersonTagProposal }) {
  const respond = useRespondToPersonProposal(session.id)
  const { data: people = [] } = usePeople()
  // The selected target: 'default' (the proposal's own resolution), 'new' (force-create), or a Person id.
  const [target, setTarget] = useState<string>('default')

  const approve = () =>
    respond.mutate({
      label: proposal.label,
      approve: true,
      bindToPersonId: target !== 'default' && target !== 'new' ? target : undefined,
      createNew: target === 'new',
    })
  const reject = () => respond.mutate({ label: proposal.label, approve: false })

  return (
    <li className="space-y-2 rounded-lg border border-border bg-surface-3 p-3">
      <div className="flex items-center gap-2">
        <span className="font-medium text-content">@{proposal.label}</span>
        {proposal.isNew ? (
          <span className="rounded-full bg-accent/15 px-2 py-0.5 text-xs font-medium text-accent">new</span>
        ) : (
          <span className="text-xs text-muted">links to @{proposal.matchedLabel}</span>
        )}
      </div>

      {proposal.contexts.length > 0 ? (
        <ul className="space-y-1">
          {proposal.contexts.map((c, i) => (
            <li key={i} className="border-l-2 border-border pl-2 text-sm text-muted">
              “{c}”
            </li>
          ))}
        </ul>
      ) : null}

      <div className="flex flex-wrap items-center gap-2">
        <label className="flex items-center gap-1 text-sm text-muted">
          Tag as
          <select
            value={target}
            onChange={(e) => setTarget(e.target.value)}
            className="rounded-lg border border-border bg-surface-2 px-2 py-1 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
          >
            <option value="default">{proposal.isNew ? `Create “${proposal.label}”` : `@${proposal.matchedLabel}`}</option>
            <option value="new">Create new “{proposal.label}”</option>
            {people.map((person) => (
              <option key={person.id} value={person.id}>
                @{person.label}
              </option>
            ))}
          </select>
        </label>
        <Button variant="primary" onPress={approve} isDisabled={respond.isPending}>
          Approve
        </Button>
        <Button onPress={reject} isDisabled={respond.isPending}>
          Reject
        </Button>
      </div>
    </li>
  )
}

export const metadataSchema = z.object({
  topics: z.string(),
  // Moods are held as a comma-joined string in the form (the chip control parses/serializes it); the
  // server resolves known-vs-custom and dedupes. Comma-joined to match the Topics form pattern. People are
  // not here — they project from the prose @-mentions (RICH-007), shown as read-only badges.
  moods: z.string(),
  // The single Activity as its canonical/custom string ('None' when unset) — single-valued, unlike Moods.
  activity: z.string(),
})

type MetadataFormValues = z.infer<typeof metadataSchema>

const { Field, applyServerErrors } = createForm<typeof metadataSchema>()

// Change-token for the metadata editor: the Session DTO carries no revision field, so derive one from
// the form values themselves. When the server changes them (accepted Suggestion, Cleanup re-run), this key
// changes and the editor remounts, re-seeding its defaults from the fresh server values. People are not a
// form field (they project from the prose), so they're excluded — the read-only badges re-render on refetch.
function metadataKey(session: Session): string {
  return JSON.stringify([session.topics, session.moods, session.activity])
}

/** Per-Session manual metadata: Topics and one-or-more Moods (known or custom free text). */
function MetadataEditor({ session }: { session: Session }) {
  const save = useSaveMetadata(session.id)

  const form = useForm({
    defaultValues: {
      topics: session.topics.join(', '),
      moods: session.moods.join(', '),
      activity: session.activity || 'None',
    } satisfies MetadataFormValues,
    validators: { onBlur: metadataSchema },
    onSubmit: async ({ value }) => {
      try {
        await save.mutateAsync({
          topics: splitList(value.topics),
          moods: splitList(value.moods),
          activity: value.activity || 'None',
        })
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
      <Form
        form={form}
        className="space-y-3 rounded-lg border border-border bg-surface-2 p-4"
      >
        <Field name="topics">{(field) => <TopicBadges field={field} />}</Field>
        <ProjectedPeople people={session.people} />
        <Field name="moods">{(field) => <MoodChips field={field} />}</Field>
        <Field name="activity">{(field) => <ActivityPicker field={field} />}</Field>
        <Form.Errors />
        <Form.Submit>Save metadata</Form.Submit>
      </Form>
    </div>
  )
}

/**
 * Read-only People badges (PRD-0006, RICH-007): the projection of who's @-mentioned in the Raw + Cleaned
 * prose, reconciled on save. Not editable here — tag People by mentioning them in either editor; remove a
 * mention to untag.
 */
function ProjectedPeople({ people }: { people: string[] }) {
  return (
    <div className="space-y-2">
      <span className="text-sm text-muted">People</span>
      {people.length > 0 ? (
        <div className="flex flex-wrap gap-2" data-testid="people-badges">
          {people.map((label) => (
            <span
              key={label}
              className="rounded-full border border-border bg-surface-3 px-3 py-0.5 text-sm text-content"
            >
              @{label}
            </span>
          ))}
        </div>
      ) : (
        <p className="text-sm text-muted">Type @ in an editor to tag someone.</p>
      )}
    </div>
  )
}

/** Multi-select Mood chips: toggle known Moods, add free-text custom Moods (PRD-0006). */
function MoodChips({ field }: { field: { state: { value: string }; handleChange: (next: string) => void } }) {
  const [draft, setDraft] = useState('')
  const selected = splitList(field.state.value)
  const has = (m: string) => selected.some((s) => s.toLowerCase() === m.toLowerCase())
  const commit = (next: string[]) => field.handleChange(next.join(', '))
  const toggle = (m: string) =>
    commit(has(m) ? selected.filter((s) => s.toLowerCase() !== m.toLowerCase()) : [...selected, m])
  const addCustom = () => {
    const value = draft.trim()
    if (value && !has(value)) commit([...selected, value])
    setDraft('')
  }
  const customs = selected.filter((s) => !KNOWN_MOODS.some((k) => k.toLowerCase() === s.toLowerCase()))

  return (
    <div className="space-y-2">
      <span className="text-sm text-muted">Moods</span>
      <div className="flex flex-wrap gap-2">
        {KNOWN_MOODS.map((m) => (
          <button
            key={m}
            type="button"
            aria-pressed={has(m)}
            onClick={() => toggle(m)}
            className={`rounded-full border px-3 py-0.5 text-sm ${
              has(m) ? 'border-accent bg-accent/15 text-content' : 'border-border bg-surface-3 text-muted hover:text-content'
            }`}
          >
            {m}
          </button>
        ))}
        {customs.map((m) => (
          <span
            key={m}
            className="flex items-center gap-1 rounded-full border border-accent bg-accent/15 py-0.5 pl-3 pr-1 text-sm text-content"
          >
            {m}
            <button type="button" aria-label={`Remove ${m}`} className="rounded-full px-1.5 text-muted hover:text-content" onClick={() => toggle(m)}>
              ✕
            </button>
          </span>
        ))}
      </div>
      <div className="flex gap-2">
        <input
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              e.preventDefault()
              addCustom()
            }
          }}
          placeholder="Add a custom mood"
          className="rounded-lg border border-border bg-surface-3 px-2 py-1 text-sm text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
        />
        <button type="button" className="text-sm text-accent hover:underline" onClick={addCustom}>
          add
        </button>
      </div>
    </div>
  )
}

/**
 * Single-select Activity picker (PRD-0007): None + the known activities + a custom free-text escape hatch,
 * each with a recognizable icon. Single-valued (unlike Moods) — selecting one replaces the prior choice.
 */
function ActivityPicker({ field }: { field: { state: { value: string }; handleChange: (next: string) => void } }) {
  const value = field.state.value.trim()
  const options = ['None', ...KNOWN_ACTIVITIES]
  const matches = (a: string) => a.toLowerCase() === value.toLowerCase()
  const isCustom = value !== '' && !options.some(matches)
  const [draft, setDraft] = useState(isCustom ? value : '')

  const commitCustom = () => {
    const next = draft.trim()
    if (next) field.handleChange(next)
  }

  return (
    <div className="space-y-2">
      <span className="text-sm text-muted">Activity</span>
      <div className="flex flex-wrap gap-2">
        {options.map((a) => {
          const active = matches(a) || (a === 'None' && value === '')
          return (
            <button
              key={a}
              type="button"
              aria-pressed={active}
              onClick={() => field.handleChange(a)}
              className={`flex items-center gap-1 rounded-full border px-3 py-0.5 text-sm ${
                active ? 'border-accent bg-accent/15 text-content' : 'border-border bg-surface-3 text-muted hover:text-content'
              }`}
            >
              <span aria-hidden>{ACTIVITY_ICONS[a]}</span>
              {a}
            </button>
          )
        })}
        {isCustom ? (
          <span className="flex items-center gap-1 rounded-full border border-accent bg-accent/15 px-3 py-0.5 text-sm text-content">
            {value}
          </span>
        ) : null}
      </div>
      <div className="flex gap-2">
        <input
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              e.preventDefault()
              commitCustom()
            }
          }}
          placeholder="Or a custom activity"
          className="rounded-lg border border-border bg-surface-3 px-2 py-1 text-sm text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
        />
        <button type="button" className="text-sm text-accent hover:underline" onClick={commitCustom}>
          set
        </button>
      </div>
    </div>
  )
}

/** Topic badge picker (PRD-0006): removable chips + a free-text input with autocomplete over prior Topics. */
function TopicBadges({ field }: { field: { state: { value: string }; handleChange: (next: string) => void } }) {
  const [draft, setDraft] = useState('')
  const { data: known = [] } = useTopics()
  const selected = splitList(field.state.value)
  const has = (t: string) => selected.some((s) => s.toLowerCase() === t.toLowerCase())
  const commit = (next: string[]) => field.handleChange(next.join(', '))
  const remove = (t: string) => commit(selected.filter((s) => s.toLowerCase() !== t.toLowerCase()))
  const add = () => {
    const value = draft.trim()
    if (value && !has(value)) commit([...selected, value]) // an unknown Topic just coins a new badge
    setDraft('')
  }
  // Autocomplete over Topics used before, minus what's already on this Session.
  const suggestions = known.filter((t) => !has(t))

  return (
    <div className="space-y-2">
      <span className="text-sm text-muted">Topics</span>
      {selected.length > 0 ? (
        <div className="flex flex-wrap gap-2">
          {selected.map((t) => (
            <span
              key={t}
              className="flex items-center gap-1 rounded-full border border-accent bg-accent/15 py-0.5 pl-3 pr-1 text-sm text-content"
            >
              #{t}
              <button type="button" aria-label={`Remove ${t}`} className="rounded-full px-1.5 text-muted hover:text-content" onClick={() => remove(t)}>
                ✕
              </button>
            </span>
          ))}
        </div>
      ) : null}
      <div className="flex gap-2">
        <input
          value={draft}
          list="topic-suggestions"
          onChange={(e) => setDraft(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              e.preventDefault()
              add()
            }
          }}
          placeholder="Add a topic"
          className="rounded-lg border border-border bg-surface-3 px-2 py-1 text-sm text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
        />
        <datalist id="topic-suggestions">
          {suggestions.map((t) => (
            <option key={t} value={t} />
          ))}
        </datalist>
        <button type="button" className="text-sm text-accent hover:underline" onClick={add}>
          add
        </button>
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

/**
 * Remount key for the Cleaned editor: scoped per Session and bumped only by a server *regeneration*
 * (Cleanup re-run or approved People-tag insertion), so the uncontrolled editor re-seeds on a
 * regeneration but NOT on the user's own debounced hand-edit saves (issue 0028). Deliberately excludes
 * `cleanedDraft` — a hand-edit save mutates it and would otherwise force a remount.
 */
export function cleanedEditorKey(session: Session): string {
  return `${session.id}:${session.cleanedRegenerationRevisionNumber}`
}

/** The AI-derived Cleaned copy, hand-editable. Edits debounce-save and append a Cleaned Revision. */
function CleanedEditor({ session }: { session: Session }) {
  const saveCleaned = useSaveCleaned(session.id)
  const mention = useMentionConfig()
  // Uncontrolled, keyed per cleaned-Revision by CleanedEditorBoundary → seed directly from server JSON.
  const onChange = useAutosave(saveCleaned.mutate) // debounced autosave

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-end">
        <SaveStatus pending={saveCleaned.isPending} success={saveCleaned.isSuccess} error={saveCleaned.isError} />
      </div>
      <RichEditor initialContent={session.cleanedDraft} onChange={onChange} className="bg-surface-3" mention={mention} />
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
          {/* Read-only render of the snapshotted JSON WITH its formatting. Keyed so switching
              versions remounts (uncontrolled editor re-seeds from the new content). */}
          <RichEditor key={viewing} initialContent={content} editable={false} />
        </div>
      ) : null}
    </div>
  )
}

function SaveStatus({ pending, success, error }: { pending: boolean; success: boolean; error: boolean }) {
  const label = error ? 'Save failed' : pending ? 'Saving…' : success ? 'Saved' : 'Autosaves as you write'
  return <span className="text-sm text-muted">{label}</span>
}
