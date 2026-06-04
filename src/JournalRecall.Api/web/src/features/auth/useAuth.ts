import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import * as authApi from './api'

const ME_KEY = ['me'] as const

/** The current session. `data` is the user, or null when signed out. */
export function useMe() {
  return useQuery({
    queryKey: ME_KEY,
    queryFn: authApi.fetchMe,
    staleTime: 60_000,
  })
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

export function useLogout() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: authApi.logout,
    onSuccess: () => queryClient.setQueryData(ME_KEY, null),
  })
}
