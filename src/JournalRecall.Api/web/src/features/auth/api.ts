import { apiFetch } from '@/shared/api/client'
import { problemError } from '@/shared/api/problem'

export interface AuthUser {
  id: string
  email: string
  roles: string[]
  /** True while the user holds a temporary password and must set their own (issue 0024). */
  mustChangePassword: boolean
}

export interface Credentials {
  email: string
  password: string
}

const jsonHeaders = { 'Content-Type': 'application/json' }

export interface AuthConfig {
  needsSetup: boolean
  selfRegistrationEnabled: boolean
}

/** Public config that drives anonymous routing (the access gate / client guard). Always reachable. */
export async function fetchAuthConfig(): Promise<AuthConfig> {
  const res = await apiFetch('/api/auth/config')
  if (!res.ok) throw new Error('Failed to load auth config')
  return res.json()
}

/** Current session, or null when unauthenticated (401). The auth cookie rides along automatically. */
export async function fetchMe(): Promise<AuthUser | null> {
  const res = await apiFetch('/api/me', { credentials: 'include' })
  if (res.status === 401) return null
  if (!res.ok) throw new Error('Failed to load session')
  return res.json()
}

/** First-run setup: creates the root Admin. 409 once the instance already has a User (PRD-0001). */
export async function setup(body: Credentials): Promise<void> {
  const res = await apiFetch('/api/setup', {
    method: 'POST',
    headers: jsonHeaders,
    body: JSON.stringify(body),
  })
  if (res.status === 409) throw new Error('This instance has already been set up.')
  if (!res.ok) throw await problemError(res, 'Setup failed')
}

export async function register(body: Credentials): Promise<void> {
  const res = await apiFetch('/api/auth/register', {
    method: 'POST',
    headers: jsonHeaders,
    credentials: 'include',
    body: JSON.stringify(body),
  })
  if (!res.ok) throw await problemError(res, 'Registration failed')
}

export async function login(body: Credentials): Promise<AuthUser> {
  const res = await apiFetch('/api/auth/login', {
    method: 'POST',
    headers: jsonHeaders,
    credentials: 'include',
    body: JSON.stringify(body),
  })
  if (res.status === 401) throw new Error('Invalid email or password')
  if (!res.ok) throw await problemError(res, 'Login failed')
  return res.json()
}

/** Set a new password (clears the forced-change flag; revokes the user's other sessions). */
export async function changePassword(body: { currentPassword: string; newPassword: string }): Promise<void> {
  const res = await apiFetch('/api/auth/change-password', {
    method: 'POST',
    headers: jsonHeaders,
    body: JSON.stringify(body),
  })
  if (!res.ok) throw await problemError(res, 'Could not change password')
}

export async function logout(): Promise<void> {
  await apiFetch('/api/auth/logout', { method: 'POST', credentials: 'include' })
}
