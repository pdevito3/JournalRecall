import { apiFetch } from '@/shared/api/client'

export interface UserSettings {
  timeZoneId: string | null
  locationCaptureEnabled: boolean
}

export async function getSettings(): Promise<UserSettings> {
  const res = await apiFetch('/api/me/settings', { credentials: 'include' })
  if (!res.ok) throw new Error('Could not load settings')
  return res.json()
}

export async function updateSettings(settings: UserSettings): Promise<void> {
  const res = await apiFetch('/api/me/settings', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify(settings),
  })
  if (!res.ok) throw new Error('Could not save settings')
}
