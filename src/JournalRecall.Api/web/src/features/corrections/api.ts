export interface Correction {
  id: string
  canonicalTerm: string
  mishearings: string[]
  hardReplace: boolean
  createdAt: string
}

export interface CorrectionForWrite {
  canonicalTerm: string
  mishearings: string[]
  hardReplace: boolean
}

export async function getCorrections(): Promise<Correction[]> {
  const res = await fetch('/api/me/corrections', { credentials: 'include' })
  if (!res.ok) throw new Error('Could not load corrections')
  return res.json()
}

export async function createCorrection(body: CorrectionForWrite): Promise<Correction> {
  const res = await fetch('/api/me/corrections', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify(body),
  })
  if (!res.ok) throw new Error('Could not create correction')
  return res.json()
}

export async function updateCorrection(id: string, body: CorrectionForWrite): Promise<void> {
  const res = await fetch(`/api/me/corrections/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify(body),
  })
  if (!res.ok) throw new Error('Could not update correction')
}

export async function deleteCorrection(id: string): Promise<void> {
  const res = await fetch(`/api/me/corrections/${id}`, {
    method: 'DELETE',
    credentials: 'include',
  })
  if (!res.ok) throw new Error('Could not delete correction')
}
