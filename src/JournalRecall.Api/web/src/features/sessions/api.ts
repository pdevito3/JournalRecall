import { apiFetch } from '@/shared/api/client'
import type {
  CleanedRevisionSummary,
  CleanupEvent,
  GeoPoint,
  Metadata,
  Revision,
  RevisionSummary,
  Session,
  SessionListItem,
  Suggestion,
} from './types'

// Types and constants live in sibling modules (FE-023 split); re-exported here so existing
// `from './api'` / `@/features/sessions/api` import sites and the barrel surface keep resolving.
export * from './types'
export * from './constants'

export async function respondToSuggestion(
  id: string,
  suggestion: Suggestion,
  action: 'accept' | 'reject',
): Promise<void> {
  const res = await apiFetch(`/api/sessions/${id}/suggestions/${action}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ kind: suggestion.kind, value: suggestion.value }),
  })
  if (!res.ok) throw new Error(`Could not ${action} suggestion`)
}

export async function saveMetadata(id: string, metadata: Metadata): Promise<void> {
  const res = await apiFetch(`/api/sessions/${id}/metadata`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify(metadata),
  })
  if (res.status === 400) throw new Error('That mood isn’t recognized')
  if (!res.ok) throw new Error('Could not save metadata')
}

export async function createSession(location?: GeoPoint): Promise<Session> {
  // A captured point is sent only when the user opted in and allowed it; otherwise a plain POST.
  const res = await apiFetch('/api/sessions', {
    method: 'POST',
    credentials: 'include',
    ...(location
      ? { headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(location) }
      : {}),
  })
  if (res.status === 401) throw new Error('Please sign in to start a session')
  if (!res.ok) throw new Error('Could not start a session')
  return res.json()
}

/** Asks the browser for one geo-point; resolves undefined if unsupported, denied, or it times out. */
export function captureLocation(): Promise<GeoPoint | undefined> {
  return new Promise((resolve) => {
    if (!('geolocation' in navigator)) return resolve(undefined)
    navigator.geolocation.getCurrentPosition(
      (pos) => resolve({ latitude: pos.coords.latitude, longitude: pos.coords.longitude }),
      () => resolve(undefined), // declined or error → no location
      { timeout: 10_000 },
    )
  })
}

export async function getSessionList(filter?: string): Promise<SessionListItem[]> {
  const url = filter ? `/api/sessions?filter=${encodeURIComponent(filter)}` : '/api/sessions'
  const res = await apiFetch(url, { credentials: 'include' })
  if (!res.ok) throw new Error('Could not load your timeline')
  return res.json()
}

export async function getSession(id: string): Promise<Session> {
  const res = await apiFetch(`/api/sessions/${id}`, { credentials: 'include' })
  if (!res.ok) throw new Error('Session not found')
  return res.json()
}

export async function getRevisions(id: string): Promise<RevisionSummary[]> {
  const res = await apiFetch(`/api/sessions/${id}/revisions`, { credentials: 'include' })
  if (!res.ok) throw new Error('Could not load history')
  return res.json()
}

export async function getRevision(id: string, revisionNumber: number): Promise<Revision> {
  const res = await apiFetch(`/api/sessions/${id}/revisions/${revisionNumber}`, { credentials: 'include' })
  if (!res.ok) throw new Error('Could not load revision')
  return res.json()
}

export async function saveDraft(id: string, rawText: string): Promise<void> {
  const res = await apiFetch(`/api/sessions/${id}/draft`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ rawText }),
  })
  if (!res.ok) throw new Error('Autosave failed')
}

/**
 * Runs AI Cleanup, streaming live progress via Server-Sent Events. Each parsed event is handed to
 * `onEvent`; the promise resolves when the stream ends (terminal event delivered).
 */
export async function streamCleanup(
  id: string,
  onEvent: (event: CleanupEvent) => void,
): Promise<void> {
  const res = await apiFetch(`/api/sessions/${id}/cleanup/stream`, {
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
  const res = await apiFetch(`/api/sessions/${id}/cleaned`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ cleanedText }),
  })
  if (!res.ok) throw new Error('Could not save cleaned copy')
}

export async function getCleanedRevisions(id: string): Promise<CleanedRevisionSummary[]> {
  const res = await apiFetch(`/api/sessions/${id}/cleaned-revisions`, { credentials: 'include' })
  if (!res.ok) throw new Error('Could not load cleaned history')
  return res.json()
}

export async function getCleanedRevision(id: string, revisionNumber: number): Promise<Revision> {
  const res = await apiFetch(`/api/sessions/${id}/cleaned-revisions/${revisionNumber}`, { credentials: 'include' })
  if (!res.ok) throw new Error('Could not load cleaned revision')
  return res.json()
}
