import { queryOptions, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { sessionKeys } from '@/features/sessions'
import * as settingsApi from './api'
import { settingsKeys } from './keys'

export function settingsQueryOptions() {
  return queryOptions({ queryKey: settingsKeys.all, queryFn: settingsApi.getSettings })
}

export function useSettings() {
  return useQuery(settingsQueryOptions())
}

export function useUpdateSettings() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: settingsApi.updateSettings,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: settingsKeys.all })
      // Timezone drives journaling-day bucketing — refresh the timeline via the owning sessions factory.
      queryClient.invalidateQueries({ queryKey: sessionKeys.all })
    },
  })
}
