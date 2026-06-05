export interface AuthUser {
  id: string
  email: string
  roles: string[]
}

export interface Credentials {
  email: string
  password: string
}

const jsonHeaders = { 'Content-Type': 'application/json' }

/** Current session, or null when unauthenticated (401). The auth cookie rides along automatically. */
export async function fetchMe(): Promise<AuthUser | null> {
  const res = await fetch('/api/me', { credentials: 'include' })
  if (res.status === 401) return null
  if (!res.ok) throw new Error('Failed to load session')
  return res.json()
}

export async function register(body: Credentials): Promise<void> {
  const res = await fetch('/api/auth/register', {
    method: 'POST',
    headers: jsonHeaders,
    credentials: 'include',
    body: JSON.stringify(body),
  })
  if (!res.ok) throw await problem(res, 'Registration failed')
}

export async function login(body: Credentials): Promise<AuthUser> {
  const res = await fetch('/api/auth/login', {
    method: 'POST',
    headers: jsonHeaders,
    credentials: 'include',
    body: JSON.stringify(body),
  })
  if (res.status === 401) throw new Error('Invalid email or password')
  if (!res.ok) throw await problem(res, 'Login failed')
  return res.json()
}

export async function logout(): Promise<void> {
  await fetch('/api/auth/logout', { method: 'POST', credentials: 'include' })
}

/** Flatten an ASP.NET ValidationProblemDetails body into a single message. */
async function problem(res: Response, fallback: string): Promise<Error> {
  try {
    const body = await res.json()
    const errors = body?.errors as Record<string, string[]> | undefined
    if (errors) return new Error(Object.values(errors).flat().join(' '))
    if (typeof body?.title === 'string') return new Error(body.title)
  } catch {
    // fall through
  }
  return new Error(fallback)
}
