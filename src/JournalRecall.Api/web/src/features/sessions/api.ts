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

export async function getSession(id: string): Promise<Session> {
  const res = await fetch(`/api/sessions/${id}`, { credentials: 'include' })
  if (!res.ok) throw new Error('Session not found')
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
