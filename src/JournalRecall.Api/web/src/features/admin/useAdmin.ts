import { queryOptions, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import * as adminApi from './api'

export function adminUsersQueryOptions() {
  return queryOptions({ queryKey: ['admin', 'users'], queryFn: adminApi.getUsers })
}

export function registrationSettingsQueryOptions() {
  return queryOptions({ queryKey: ['admin', 'registration'], queryFn: adminApi.getRegistration })
}

export function aiProviderQueryOptions() {
  return queryOptions({ queryKey: ['admin', 'ai-provider'], queryFn: adminApi.getAiProvider })
}

export function useAdminUsers() {
  return useQuery(adminUsersQueryOptions())
}

function useUsersMutation<TArgs>(fn: (args: TArgs) => Promise<void>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'users'] }),
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
  return useQuery(registrationSettingsQueryOptions())
}

export function useUpdateRegistration() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: adminApi.updateRegistration,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'registration'] })
      // The public auth config exposes the same flag (conditional register link / route guard).
      queryClient.invalidateQueries({ queryKey: ['auth', 'config'] })
    },
  })
}

export function useAiProvider() {
  return useQuery(aiProviderQueryOptions())
}

export function useUpdateAiProvider() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: adminApi.updateAiProvider,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'ai-provider'] }),
  })
}
