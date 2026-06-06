import type { ReactNode } from 'react'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import { useSetup } from './useAuth'
import * as authApi from './api'

/**
 * Regression guard for issue 0025: completing setup must invalidate the cached auth-config so the route
 * guard re-reads `needsSetup:false` on the post-setup navigation instead of bouncing the just-created
 * operator back to /setup off a stale `needsSetup:true`.
 */
describe('useSetup', () => {
  beforeEach(() => vi.restoreAllMocks())

  function wrap(queryClient: QueryClient) {
    return ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    )
  }

  it('invalidates the cached auth-config (needsSetup) on success', async () => {
    vi.spyOn(authApi, 'setup').mockResolvedValue(undefined)
    const queryClient = new QueryClient()
    // Prime the cache the way the route guard would have, pre-setup.
    queryClient.setQueryData(['auth', 'config'], { needsSetup: true, selfRegistrationEnabled: false })
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')

    const { result } = renderHook(() => useSetup(), { wrapper: wrap(queryClient) })
    await result.current.mutateAsync({ email: 'a', password: 'b' } as never)

    await waitFor(() =>
      expect(invalidate).toHaveBeenCalledWith({ queryKey: ['auth', 'config'] }),
    )
  })
})
