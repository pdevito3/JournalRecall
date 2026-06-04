export interface Session {
  id: string
  createdAt: string
  rawDraft: string
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
