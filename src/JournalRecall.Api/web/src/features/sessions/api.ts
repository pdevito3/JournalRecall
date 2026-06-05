export type CleanupStatus = 'NotRun' | 'Running' | 'Clean' | 'Stale' | 'Failed'

/** The app-defined known moods (mirrors the server's Mood.Known). */
export const KNOWN_MOODS = [
  'Joyful',
  'Content',
  'Calm',
  'Neutral',
  'Tired',
  'Anxious',
  'Sad',
  'Angry',
  'Excited',
  'Grateful',
] as const

export interface Mood {
  key: string
  customValue: string | null
}

export interface Metadata {
  topics: string[]
  people: string[]
  mood: Mood | null
}

export type SuggestionKind = 'Topic' | 'Person' | 'Mood'

export interface Suggestion {
  kind: SuggestionKind
  value: string
  moodCustomValue: string | null
}

export interface Session {
  id: string
  createdAt: string
  rawDraft: string
  cleanedDraft: string
  synopsis: string
  cleanupStatus: CleanupStatus
  cleanedHasHandEdits: boolean
  topics: string[]
  people: string[]
  mood: Mood | null
  suggestions: Suggestion[]
}

export async function respondToSuggestion(
  id: string,
  suggestion: Suggestion,
  action: 'accept' | 'reject',
): Promise<void> {
  const res = await fetch(`/api/sessions/${id}/suggestions/${action}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ kind: suggestion.kind, value: suggestion.value }),
  })
  if (!res.ok) throw new Error(`Could not ${action} suggestion`)
}

export async function saveMetadata(id: string, metadata: Metadata): Promise<void> {
  const res = await fetch(`/api/sessions/${id}/metadata`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify(metadata),
  })
  if (res.status === 400) throw new Error('That mood isn’t recognized')
  if (!res.ok) throw new Error('Could not save metadata')
}

export async function createSession(): Promise<Session> {
  const res = await fetch('/api/sessions', { method: 'POST', credentials: 'include' })
  if (res.status === 401) throw new Error('Please sign in to start a session')
  if (!res.ok) throw new Error('Could not start a session')
  return res.json()
}

export interface SessionListItem {
  id: string
  createdAt: string
  journalingDay: string // YYYY-MM-DD in the user's timezone
  preview: string
  topics: string[]
  people: string[]
  mood: Mood | null
}

export async function getSessionList(filter?: string): Promise<SessionListItem[]> {
  const url = filter ? `/api/sessions?filter=${encodeURIComponent(filter)}` : '/api/sessions'
  const res = await fetch(url, { credentials: 'include' })
  if (!res.ok) throw new Error('Could not load your timeline')
  return res.json()
}

export async function getSession(id: string): Promise<Session> {
  const res = await fetch(`/api/sessions/${id}`, { credentials: 'include' })
  if (!res.ok) throw new Error('Session not found')
  return res.json()
}

export interface RevisionSummary {
  revisionNumber: number
  createdAt: string
}

export interface Revision {
  revisionNumber: number
  createdAt: string
  content: string
}

export async function getRevisions(id: string): Promise<RevisionSummary[]> {
  const res = await fetch(`/api/sessions/${id}/revisions`, { credentials: 'include' })
  if (!res.ok) throw new Error('Could not load history')
  return res.json()
}

export async function getRevision(id: string, revisionNumber: number): Promise<Revision> {
  const res = await fetch(`/api/sessions/${id}/revisions/${revisionNumber}`, { credentials: 'include' })
  if (!res.ok) throw new Error('Could not load revision')
  return res.json()
}

export async function saveDraft(id: string, rawText: string): Promise<void> {
  const res = await fetch(`/api/sessions/${id}/draft`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ rawText }),
  })
  if (!res.ok) throw new Error('Autosave failed')
}

/** A projected agent lifecycle event from the Cleanup SSE stream (the stable wire envelope). */
export interface CleanupEvent {
  type: string
}

/**
 * Runs AI Cleanup, streaming live progress via Server-Sent Events. Each parsed event is handed to
 * `onEvent`; the promise resolves when the stream ends (terminal event delivered).
 */
export async function streamCleanup(
  id: string,
  onEvent: (event: CleanupEvent) => void,
): Promise<void> {
  const res = await fetch(`/api/sessions/${id}/cleanup/stream`, {
    method: 'POST',
    credentials: 'include',
  })
  if (!res.ok || !res.body) throw new Error('Could not start cleanup')

  const reader = res.body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''

  for (;;) {
    const { done, value } = await reader.read()
    if (done) break
    buffer += decoder.decode(value, { stream: true })

    // SSE frames are separated by a blank line; each carries one `data: {json}` line.
    let split: number
    while ((split = buffer.indexOf('\n\n')) >= 0) {
      const frame = buffer.slice(0, split)
      buffer = buffer.slice(split + 2)
      const line = frame.split('\n').find((l) => l.startsWith('data:'))
      if (!line) continue
      try {
        onEvent(JSON.parse(line.slice('data:'.length).trim()) as CleanupEvent)
      } catch {
        // Ignore a partial/garbled frame; the stream continues.
      }
    }
  }
}

export async function saveCleaned(id: string, cleanedText: string): Promise<void> {
  const res = await fetch(`/api/sessions/${id}/cleaned`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ cleanedText }),
  })
  if (!res.ok) throw new Error('Could not save cleaned copy')
}

export interface CleanedRevisionSummary {
  revisionNumber: number
  createdAt: string
}

export async function getCleanedRevisions(id: string): Promise<CleanedRevisionSummary[]> {
  const res = await fetch(`/api/sessions/${id}/cleaned-revisions`, { credentials: 'include' })
  if (!res.ok) throw new Error('Could not load cleaned history')
  return res.json()
}

export async function getCleanedRevision(id: string, revisionNumber: number): Promise<Revision> {
  const res = await fetch(`/api/sessions/${id}/cleaned-revisions/${revisionNumber}`, { credentials: 'include' })
  if (!res.ok) throw new Error('Could not load cleaned revision')
  return res.json()
}
