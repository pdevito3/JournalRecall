import { queryOptions, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { z } from 'zod'
import type { SummaryPeriod } from './api'
import * as summariesApi from './api'
import { summaryKeys } from './keys'

/** Today as a YYYY-MM-DD anchor — the default the URL falls back to when no date is given. */
export function todayYmd(): string {
  const now = new Date()
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`
}

/**
 * Summary period + anchor date as URL search state (FE-010): a period roll-up becomes a shareable,
 * refresh-surviving link. `.catch(...)` makes malformed params normalize to defaults instead of
 * throwing, so a bad URL never crashes the route. The date default is computed per-parse (today).
 */
export const summarySearchSchema = z.object({
  period: z.enum(['Day', 'Week', 'Month', 'Quarter', 'Year']).catch('Day').default('Day'),
  date: z
    .string()
    .regex(/^\d{4}-\d{2}-\d{2}$/)
    .catch(() => todayYmd())
    .default(() => todayYmd()),
})

export type SummarySearch = z.infer<typeof summarySearchSchema>

export function summaryQueryOptions(period: SummaryPeriod, date: string) {
  return queryOptions({
    queryKey: summaryKeys.detail(period, date),
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
    onSuccess: (summary) => queryClient.setQueryData(summaryKeys.detail(period, date), summary),
  })
}
