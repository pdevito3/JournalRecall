import { queryOptions, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import * as correctionsApi from './api'

const key = ['corrections']

export function correctionsQueryOptions() {
  return queryOptions({ queryKey: key, queryFn: correctionsApi.getCorrections })
}

export function useCorrections() {
  return useQuery(correctionsQueryOptions())
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
