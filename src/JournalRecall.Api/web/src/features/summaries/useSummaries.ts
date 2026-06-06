import { queryOptions, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { SummaryPeriod } from './api'
import * as summariesApi from './api'

export function summaryQueryOptions(period: SummaryPeriod, date: string) {
  return queryOptions({
    queryKey: ['summary', period, date],
    queryFn: () => summariesApi.getSummary(period, date),
  })
}

export function useSummary(period: SummaryPeriod, date: string) {
  return useQuery(summaryQueryOptions(period, date))
}

export function useGenerateSummary(period: SummaryPeriod, date: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => summariesApi.generateSummary(period, date),
    // The freshly generated Summary is the canonical state — prime the read cache with it.
    onSuccess: (summary) => queryClient.setQueryData(['summary', period, date], summary),
  })
}
