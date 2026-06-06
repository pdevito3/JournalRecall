// Sessions feature — wire/domain types. Pure type declarations (no runtime code);
// split out of `api.ts` per FE-023 so the module is types / constants / fetchers.
// Re-exported from `api.ts` so existing intra-feature imports keep resolving.

export type CleanupStatus = 'NotRun' | 'Running' | 'Clean' | 'Stale' | 'Failed'

export interface Metadata {
  topics: string[]
  people: string[]
  // Moods are plain strings: a known mood name or free-text custom mood (PRD-0006).
  moods: string[]
}

export type SuggestionKind = 'Topic' | 'Person' | 'Mood'

export interface Suggestion {
  kind: SuggestionKind
  // For a Mood, the value is the known mood name or custom text; Topics carry their name.
  value: string
}

/** A captured geo-point (CONTEXT.md Location): coordinates only. */
export interface GeoPoint {
  latitude: number
  longitude: number
}

export interface Session {
  id: string
  createdAt: string
  rawDraft: string
  cleanedDraft: string
  synopsis: string
  cleanupStatus: CleanupStatus
  cleanedHasHandEdits: boolean
  topics: string[]
  people: string[]
  moods: string[]
  suggestions: Suggestion[]
  location: GeoPoint | null
}

export interface SessionListItem {
  id: string
  createdAt: string
  journalingDay: string // YYYY-MM-DD in the user's timezone
  preview: string
  topics: string[]
  people: string[]
  moods: string[]
}

export interface RevisionSummary {
  revisionNumber: number
  createdAt: string
}

export interface Revision {
  revisionNumber: number
  createdAt: string
  content: string
}

/** A projected agent lifecycle event from the Cleanup SSE stream (the stable wire envelope). */
export interface CleanupEvent {
  type: string
}

export interface CleanedRevisionSummary {
  revisionNumber: number
  createdAt: string
}
