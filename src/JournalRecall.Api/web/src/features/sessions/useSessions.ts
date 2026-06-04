import { useMutation, useQuery } from '@tanstack/react-query'
import * as sessionsApi from './api'

export function useCreateSession() {
  return useMutation({ mutationFn: sessionsApi.createSession })
}

export function useSession(id: string) {
  return useQuery({
    queryKey: ['session', id],
    queryFn: () => sessionsApi.getSession(id),
  })
}

export function useSaveDraft(id: string) {
  return useMutation({ mutationFn: (rawText: string) => sessionsApi.saveDraft(id, rawText) })
}
