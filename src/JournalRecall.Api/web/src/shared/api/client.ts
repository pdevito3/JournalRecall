/**
 * The single fetch wrapper every API call goes through (ADR-0005). It does two jobs:
 *
 * 1. **CSRF**: sends the custom `X-CSRF` header on mutating requests, which the server requires as
 *    defense-in-depth over `SameSite=Strict`. A browser can't set this header cross-origin without a
 *    CORS preflight the server never approves.
 * 2. **Silent, single-flight refresh**: a 401 mid-session means the short access token expired, so it
 *    transparently `POST`s `/api/auth/refresh` and retries the original request once. Concurrent 401s
 *    coalesce into a *single* in-flight refresh, so a burst of expired calls never fires a thundering
 *    herd of refreshes or falsely logs the user out.
 */

const SAFE_METHODS = new Set(['GET', 'HEAD', 'OPTIONS', 'TRACE'])

/** The one refresh in flight, shared by every caller that 401s while it runs; null when idle. */
let inFlightRefresh: Promise<boolean> | null = null

function refreshOnce(): Promise<boolean> {
  inFlightRefresh ??= fetch('/api/auth/refresh', {
    method: 'POST',
    credentials: 'include',
    headers: { 'X-CSRF': '1' },
  })
    .then((res) => res.ok)
    .catch(() => false)
    .finally(() => {
      inFlightRefresh = null
    })
  return inFlightRefresh
}

function buildInit(init: RequestInit): RequestInit {
  const method = (init.method ?? 'GET').toUpperCase()
  const headers = new Headers(init.headers)
  if (!SAFE_METHODS.has(method)) headers.set('X-CSRF', '1')
  return { credentials: 'include', ...init, headers }
}

export async function apiFetch(input: string, init: RequestInit = {}): Promise<Response> {
  const finalInit = buildInit(init)
  const res = await fetch(input, finalInit)

  // Don't try to refresh the auth endpoints themselves (a 401 there is real, and refreshing would recurse).
  if (res.status !== 401 || input.startsWith('/api/auth/')) return res

  const refreshed = await refreshOnce()
  if (!refreshed) return res
  return fetch(input, finalInit)
}
