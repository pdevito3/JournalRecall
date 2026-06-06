import { useCallback, useState } from 'react'
import { queryOptions, useMutation, useQuery, useQueryClient, useSuspenseQuery } from '@tanstack/react-query'
import { z } from 'zod'
import { KNOWN_MOODS } from './api'
import * as sessionsApi from './api'

/**
 * Timeline filters as URL search state (FE-009): a filtered view is shareable, bookmarkable, and
 * refresh/back-button safe. `.catch('')` makes malformed params normalize to defaults instead of
 * throwing, so a bad URL never crashes the route.
 */
export const timelineSearchSchema = z.object({
  topic: z.string().catch('').default(''),
  mood: z.enum(KNOWN_MOODS).or(z.literal('')).catch('').default(''),
})

export type TimelineSearch = z.infer<typeof timelineSearchSchema>

/**
 * Build a QueryKit filter string from the timeline filters (undefined when nothing is set). There is no
 * person filter: People are directory references now, so a PersonId-based filter is a future slice (PRD-0006).
 */
export function buildSessionFilter({ topic, mood }: TimelineSearch): string | undefined {
  const parts: string[] = []
  if (topic.trim()) parts.push(`topics == "${topic.trim()}"`)
  if (mood) parts.push(`mood == "${mood}"`)
  return parts.length > 0 ? parts.join(' && ') : undefined
}

export function sessionListQueryOptions(filter?: string) {
  return queryOptions({
    queryKey: ['sessions', filter ?? null],
    queryFn: () => sessionsApi.getSessionList(filter),
  })
}

export function sessionQueryOptions(id: string) {
  return queryOptions({
    queryKey: ['session', id],
    queryFn: () => sessionsApi.getSession(id),
  })
}

export function revisionsQueryOptions(id: string) {
  return queryOptions({
    queryKey: ['session', id, 'revisions'],
    queryFn: () => sessionsApi.getRevisions(id),
  })
}

export function revisionQueryOptions(id: string, revisionNumber: number | null) {
  return queryOptions({
    queryKey: ['session', id, 'revisions', revisionNumber],
    queryFn: () => sessionsApi.getRevision(id, revisionNumber!),
    enabled: revisionNumber != null,
  })
}

export function cleanedRevisionsQueryOptions(id: string) {
  return queryOptions({
    queryKey: ['session', id, 'cleaned-revisions'],
    queryFn: () => sessionsApi.getCleanedRevisions(id),
  })
}

export function cleanedRevisionQueryOptions(id: string, revisionNumber: number | null) {
  return queryOptions({
    queryKey: ['session', id, 'cleaned-revisions', revisionNumber],
    queryFn: () => sessionsApi.getCleanedRevision(id, revisionNumber!),
    enabled: revisionNumber != null,
  })
}

export function useCreateSession() {
  return useMutation({
    mutationFn: (location?: sessionsApi.GeoPoint) => sessionsApi.createSession(location),
  })
}

export function useSessionList(filter?: string) {
  return useQuery(sessionListQueryOptions(filter))
}

// Primed (awaited) by the Session-detail route loader — read via Suspense so the router's default
// pending/error components own the loading and failure states (FE-011). The Revision streams stay on
// useQuery because the loader only prefetches them (non-awaited) — they must tolerate not-yet-loaded.
export function useSession(id: string) {
  return useSuspenseQuery(sessionQueryOptions(id))
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
  return useQuery(revisionsQueryOptions(id))
}

export function useRevision(id: string, revisionNumber: number | null) {
  return useQuery(revisionQueryOptions(id, revisionNumber))
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
  return useQuery(cleanedRevisionsQueryOptions(id))
}

export function useCleanedRevision(id: string, revisionNumber: number | null) {
  return useQuery(cleanedRevisionQueryOptions(id, revisionNumber))
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
