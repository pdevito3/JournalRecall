import { apiFetch } from '@/shared/api/client'

export interface AdminUser {
  id: string
  email: string
  roles: string[]
  isDisabled: boolean
}

export const ROLES = ['Member', 'Admin'] as const
export const PROVIDERS = ['OpenAI', 'AzureOpenAI'] as const

export interface AiProvider {
  provider: string
  endpoint: string | null
  model: string
  hasApiKey: boolean
}

export interface AiProviderInput {
  provider: string
  endpoint: string | null
  apiKey: string | null
  model: string
}

async function ok(res: Response, message: string): Promise<void> {
  if (!res.ok) throw new Error(message)
}

export async function getUsers(): Promise<AdminUser[]> {
  const res = await apiFetch('/api/admin/users', { credentials: 'include' })
  await ok(res, 'Could not load users')
  return res.json()
}

export async function createUser(input: { email: string; password: string; role: string }): Promise<void> {
  const res = await apiFetch('/api/admin/users', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify(input),
  })
  if (res.status === 400) {
    const body = await res.json().catch(() => null)
    const errors = body?.errors as Record<string, string[]> | undefined
    throw new Error(errors ? Object.values(errors).flat().join(' ') : 'Could not create user')
  }
  await ok(res, 'Could not create user')
}

export async function setUserRole(id: string, role: string): Promise<void> {
  const res = await apiFetch(`/api/admin/users/${id}/role`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ role }),
  })
  await ok(res, 'Could not change role')
}

export async function setUserDisabled(id: string, disabled: boolean): Promise<void> {
  const res = await apiFetch(`/api/admin/users/${id}/${disabled ? 'disable' : 'enable'}`, {
    method: 'POST',
    credentials: 'include',
  })
  await ok(res, 'Could not update user')
}

export interface RegistrationSettings {
  selfRegistrationEnabled: boolean
}

export async function getRegistration(): Promise<RegistrationSettings> {
  const res = await apiFetch('/api/admin/registration', { credentials: 'include' })
  await ok(res, 'Could not load registration setting')
  return res.json()
}

export async function updateRegistration(input: RegistrationSettings): Promise<void> {
  const res = await apiFetch('/api/admin/registration', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify(input),
  })
  await ok(res, 'Could not save registration setting')
}

export async function getAiProvider(): Promise<AiProvider> {
  const res = await apiFetch('/api/admin/ai-provider', { credentials: 'include' })
  await ok(res, 'Could not load AI provider')
  return res.json()
}

export async function updateAiProvider(input: AiProviderInput): Promise<void> {
  const res = await apiFetch('/api/admin/ai-provider', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify(input),
  })
  await ok(res, 'Could not save AI provider')
}
