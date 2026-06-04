import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
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
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (rawText: string) => sessionsApi.saveDraft(id, rawText),
    // A save point may have appended a Revision — refresh the history.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['session', id, 'revisions'] }),
  })
}

export function useRevisions(id: string) {
  return useQuery({
    queryKey: ['session', id, 'revisions'],
    queryFn: () => sessionsApi.getRevisions(id),
  })
}

export function useRevision(id: string, revisionNumber: number | null) {
  return useQuery({
    queryKey: ['session', id, 'revisions', revisionNumber],
    queryFn: () => sessionsApi.getRevision(id, revisionNumber!),
    enabled: revisionNumber != null,
  })
}
