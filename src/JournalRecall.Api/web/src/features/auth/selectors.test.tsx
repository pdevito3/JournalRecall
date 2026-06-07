import type { ReactNode } from 'react'
import { describe, expect, it } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook } from '@testing-library/react'
import { selectIsAdmin, selectRoles, useAuthRoles, useIsAdmin } from './useAuth'
import { authKeys } from './keys'
import type { AuthUser } from './api'

/**
 * The Admin-role rule lives in exactly one place (FE-003). These tests pin the module-level selectors
 * across the representative `me` payloads (admin / member / signed-out) and assert the derived hooks
 * surface the same selected slice off the primed `['me']` cache.
 */
const admin: AuthUser = { id: '1', username: 'root', roles: ['Admin', 'Member'], mustChangePassword: false }
const member: AuthUser = { id: '2', username: 'jo', roles: ['Member'], mustChangePassword: false }

describe('auth selectors', () => {
  it('selectRoles returns the roles for a user and [] when signed out', () => {
    expect(selectRoles(admin)).toEqual(['Admin', 'Member'])
    expect(selectRoles(member)).toEqual(['Member'])
    expect(selectRoles(null)).toEqual([])
    expect(selectRoles(undefined)).toEqual([])
  })

  it('selectIsAdmin is true only when the Admin role is present', () => {
    expect(selectIsAdmin(admin)).toBe(true)
    expect(selectIsAdmin(member)).toBe(false)
    expect(selectIsAdmin(null)).toBe(false)
    expect(selectIsAdmin(undefined)).toBe(false)
  })
})

describe('derived auth hooks', () => {
  function wrap(me: AuthUser | null) {
    const queryClient = new QueryClient()
    queryClient.setQueryData(authKeys.me, me)
    return ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    )
  }

  it('useAuthRoles exposes the roles slice', () => {
    expect(renderHook(() => useAuthRoles(), { wrapper: wrap(admin) }).result.current).toEqual([
      'Admin',
      'Member',
    ])
    expect(renderHook(() => useAuthRoles(), { wrapper: wrap(member) }).result.current).toEqual(['Member'])
    expect(renderHook(() => useAuthRoles(), { wrapper: wrap(null) }).result.current).toEqual([])
  })

  it('useIsAdmin exposes the Admin-role rule', () => {
    expect(renderHook(() => useIsAdmin(), { wrapper: wrap(admin) }).result.current).toBe(true)
    expect(renderHook(() => useIsAdmin(), { wrapper: wrap(member) }).result.current).toBe(false)
    expect(renderHook(() => useIsAdmin(), { wrapper: wrap(null) }).result.current).toBe(false)
  })
})
