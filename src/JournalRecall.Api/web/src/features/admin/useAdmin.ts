import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import * as adminApi from './api'

export function useAdminUsers() {
  return useQuery({ queryKey: ['admin', 'users'], queryFn: adminApi.getUsers })
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

export function useSetUserDisabled() {
  return useUsersMutation(({ id, disabled }: { id: string; disabled: boolean }) =>
    adminApi.setUserDisabled(id, disabled),
  )
}

export function useRegistrationSettings() {
  return useQuery({ queryKey: ['admin', 'registration'], queryFn: adminApi.getRegistration })
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
  return useQuery({ queryKey: ['admin', 'ai-provider'], queryFn: adminApi.getAiProvider })
}

export function useUpdateAiProvider() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: adminApi.updateAiProvider,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'ai-provider'] }),
  })
}
