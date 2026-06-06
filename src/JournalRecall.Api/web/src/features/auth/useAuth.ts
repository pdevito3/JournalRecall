import { queryOptions, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import * as authApi from './api'

const ME_KEY = ['me'] as const

/**
 * Single source of truth for the current-session query: key, `queryFn`, and `staleTime` defined once.
 * Consumed by `useMe` and the root route's `beforeLoad` so the access gate and components never
 * disagree on the cache.
 */
export function meQueryOptions() {
  return queryOptions({
    queryKey: ME_KEY,
    queryFn: authApi.fetchMe,
    staleTime: 60_000,
  })
}

/**
 * Single source of truth for the public auth-config query (needs-setup / self-registration).
 * Shared by `useAuthConfig` and the root route guard's cache.
 */
export function authConfigQueryOptions() {
  return queryOptions({
    queryKey: ['auth', 'config'],
    queryFn: authApi.fetchAuthConfig,
    staleTime: 30_000,
  })
}

/** The current session. `data` is the user, or null when signed out. */
export function useMe() {
  return useQuery(meQueryOptions())
}

export function useLogin() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: authApi.login,
    onSuccess: (user) => queryClient.setQueryData(ME_KEY, user),
  })
}

export function useRegister() {
  return useMutation({ mutationFn: authApi.register })
}

export function useSetup() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: authApi.setup,
    // Setup creates the root Admin, flipping needsSetup → false. Invalidate the cached auth-config
    // (shared with the route guard) so the post-setup navigation re-reads needsSetup:false and the
    // guard doesn't bounce the just-created operator back to /setup (issue 0025).
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['auth', 'config'] }),
  })
}

/** Public instance config (needs-setup / self-registration), shared with the route guard's cache. */
export function useAuthConfig() {
  return useQuery(authConfigQueryOptions())
}

export function useChangePassword() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: authApi.changePassword,
    // Refetch the session so the forced-change flag (and the guard) release.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ME_KEY }),
  })
}

export function useLogout() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: authApi.logout,
    onSuccess: () => queryClient.setQueryData(ME_KEY, null),
  })
}
