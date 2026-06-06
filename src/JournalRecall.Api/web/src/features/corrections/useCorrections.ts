import { queryOptions, useMutation, useQueryClient, useSuspenseQuery } from '@tanstack/react-query'
import * as correctionsApi from './api'

const key = ['corrections']

export function correctionsQueryOptions() {
  return queryOptions({ queryKey: key, queryFn: correctionsApi.getCorrections })
}

// Primed (awaited) by the Corrections route loader — read via Suspense so the router's default
// pending/error components own the loading and failure states (FE-011).
export function useCorrections() {
  return useSuspenseQuery(correctionsQueryOptions())
}

export function useCreateCorrection() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: correctionsApi.createCorrection,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: key }),
  })
}

export function useUpdateCorrection() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: correctionsApi.CorrectionForWrite }) =>
      correctionsApi.updateCorrection(id, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: key }),
  })
}

export function useDeleteCorrection() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: correctionsApi.deleteCorrection,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: key }),
  })
}
