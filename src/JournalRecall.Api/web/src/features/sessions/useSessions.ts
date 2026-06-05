import { useCallback, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import * as sessionsApi from './api'

export function useCreateSession() {
  return useMutation({ mutationFn: sessionsApi.createSession })
}

export function useSessionList(filter?: string) {
  return useQuery({
    queryKey: ['sessions', filter ?? null],
    queryFn: () => sessionsApi.getSessionList(filter),
  })
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

export function useSaveMetadata(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (metadata: sessionsApi.Metadata) => sessionsApi.saveMetadata(id, metadata),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['session', id] })
      queryClient.invalidateQueries({ queryKey: ['sessions'] }) // timeline chips/filters
    },
  })
}

export function useRespondToSuggestion(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ suggestion, action }: { suggestion: sessionsApi.Suggestion; action: 'accept' | 'reject' }) =>
      sessionsApi.respondToSuggestion(id, suggestion, action),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['session', id] })
      queryClient.invalidateQueries({ queryKey: ['sessions'] }) // accepted tags show on the timeline
    },
  })
}

export function useSaveCleaned(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (cleanedText: string) => sessionsApi.saveCleaned(id, cleanedText),
    // A hand-edit appended a Cleaned Revision and flipped the hand-edit flag — refresh both.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['session', id] }),
  })
}

export function useCleanedRevisions(id: string) {
  return useQuery({
    queryKey: ['session', id, 'cleaned-revisions'],
    queryFn: () => sessionsApi.getCleanedRevisions(id),
  })
}

export function useCleanedRevision(id: string, revisionNumber: number | null) {
  return useQuery({
    queryKey: ['session', id, 'cleaned-revisions', revisionNumber],
    queryFn: () => sessionsApi.getCleanedRevision(id, revisionNumber!),
    enabled: revisionNumber != null,
  })
}

/**
 * Drives an AI Cleanup run, exposing live progress streamed from the server. On completion the cached
 * Session + its Cleaned history are invalidated so the side-by-side view and status badge refresh.
 */
export function useCleanup(id: string) {
  const queryClient = useQueryClient()
  const [running, setRunning] = useState(false)
  const [progress, setProgress] = useState<string[]>([])
  const [error, setError] = useState(false)

  const run = useCallback(async () => {
    setRunning(true)
    setError(false)
    setProgress([])
    try {
      await sessionsApi.streamCleanup(id, (event) => {
        // Surface a short human line per lifecycle event (progress, not a static spinner).
        setProgress((prior) => [...prior, labelEvent(event.type)])
      })
    } catch {
      setError(true)
    } finally {
      setRunning(false)
      await queryClient.invalidateQueries({ queryKey: ['session', id] })
    }
  }, [id, queryClient])

  return { run, running, progress, error }
}

function labelEvent(type: string): string {
  switch (type) {
    case 'run.started':
      return 'Starting cleanup…'
    case 'turn.started':
      return 'Reading your words…'
    case 'usage.updated':
      return 'Polishing…'
    case 'completed':
      return 'Done.'
    case 'stopped':
      return 'Stopped.'
    case 'failed':
      return 'Cleanup failed.'
    default:
      return type
  }
}
