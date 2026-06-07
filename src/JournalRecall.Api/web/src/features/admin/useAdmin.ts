import { queryOptions, useMutation, useQueryClient, useSuspenseQuery } from '@tanstack/react-query'
import { authKeys } from '@/features/auth'
import * as adminApi from './api'
import { adminKeys } from './keys'

export function adminUsersQueryOptions() {
  return queryOptions({ queryKey: adminKeys.users(), queryFn: adminApi.getUsers })
}

export function registrationSettingsQueryOptions() {
  return queryOptions({ queryKey: adminKeys.registration(), queryFn: adminApi.getRegistration })
}

export function aiProviderQueryOptions() {
  return queryOptions({ queryKey: adminKeys.aiProvider(), queryFn: adminApi.getAiProvider })
}

// Primed (awaited) by the Admin route loader — read via Suspense so the router's default pending/error
// components own the loading and failure states (FE-011).
export function useAdminUsers() {
  return useSuspenseQuery(adminUsersQueryOptions())
}

function useUsersMutation<TArgs>(fn: (args: TArgs) => Promise<void>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: adminKeys.users() }),
  })
}

export function useCreateUser() {
  return useUsersMutation(adminApi.createUser)
}

export function useSetUserRole() {
  return useUsersMutation(({ id, role }: { id: string; role: string }) => adminApi.setUserRole(id, role))
}

export function useResetUserPassword() {
  return useUsersMutation(({ id, password }: { id: string; password: string }) =>
    adminApi.resetUserPassword(id, password),
  )
}

export function useSetUserDisabled() {
  return useUsersMutation(({ id, disabled }: { id: string; disabled: boolean }) =>
    adminApi.setUserDisabled(id, disabled),
  )
}

export function useRegistrationSettings() {
  return useSuspenseQuery(registrationSettingsQueryOptions())
}

export function useUpdateRegistration() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: adminApi.updateRegistration,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: adminKeys.registration() })
      // The public auth config exposes the same flag (conditional register link / route guard) — invalidate
      // via the owning auth feature's factory so the key isn't re-typed here.
      queryClient.invalidateQueries({ queryKey: authKeys.config })
    },
  })
}

export function useAiProvider() {
  return useSuspenseQuery(aiProviderQueryOptions())
}

export function useUpdateAiProvider() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: adminApi.updateAiProvider,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: adminKeys.aiProvider() }),
  })
}
