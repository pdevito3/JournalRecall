import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiFetch } from './client'

function response(status: number): Response {
  return { ok: status >= 200 && status < 300, status } as Response
}

afterEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

describe('apiFetch', () => {
  it('sends the X-CSRF header on mutating requests but not on GETs', async () => {
    const fetchMock = vi.fn((_input: string, _init?: RequestInit) => Promise.resolve(response(200)))
    vi.stubGlobal('fetch', fetchMock)

    await apiFetch('/api/sessions', { method: 'POST' })
    await apiFetch('/api/sessions')

    const post = new Headers(fetchMock.mock.calls[0]![1]?.headers)
    const get = new Headers(fetchMock.mock.calls[1]![1]?.headers)
    expect(post.get('X-CSRF')).toBe('1')
    expect(get.has('X-CSRF')).toBe(false)
  })

  it('coalesces concurrent 401s into a single refresh, then retries each original request', async () => {
    let refreshed = false
    let refreshCalls = 0
    const fetchMock = vi.fn((input: string, _init?: RequestInit) => {
      if (input === '/api/auth/refresh') {
        refreshCalls++
        refreshed = true
        return Promise.resolve(response(200))
      }
      return Promise.resolve(response(refreshed ? 200 : 401))
    })
    vi.stubGlobal('fetch', fetchMock)

    // Two protected calls both hit an expired access token at once.
    const [a, b] = await Promise.all([apiFetch('/api/sessions'), apiFetch('/api/me')])

    expect(refreshCalls).toBe(1) // single-flight: one shared refresh, not two
    expect(a.status).toBe(200) // original request retried successfully
    expect(b.status).toBe(200)
  })

  it('does not attempt to refresh the auth endpoints themselves', async () => {
    const fetchMock = vi.fn((_input: string, _init?: RequestInit) => Promise.resolve(response(401)))
    vi.stubGlobal('fetch', fetchMock)

    const res = await apiFetch('/api/auth/login', { method: 'POST' })

    expect(res.status).toBe(401)
    expect(fetchMock).toHaveBeenCalledTimes(1) // no refresh, no retry
  })
})
