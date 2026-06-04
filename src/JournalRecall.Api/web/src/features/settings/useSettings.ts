import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import * as settingsApi from './api'

export function useSettings() {
  return useQuery({ queryKey: ['settings'], queryFn: settingsApi.getSettings })
}

export function useUpdateSettings() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: settingsApi.updateSettings,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['settings'] })
      // Timezone drives journaling-day bucketing — refresh the timeline.
      queryClient.invalidateQueries({ queryKey: ['sessions'] })
    },
  })
}
