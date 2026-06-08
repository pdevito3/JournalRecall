// Sessions feature — wire/domain types. Pure type declarations (no runtime code);
// split out of `api.ts` per FE-023 so the module is types / constants / fetchers.
// Re-exported from `api.ts` so existing intra-feature imports keep resolving.

export type CleanupStatus = 'NotRun' | 'Running' | 'Clean' | 'Stale' | 'Failed'

export interface Metadata {
  topics: string[]
  // Moods are plain strings: a known mood name or free-text custom mood (PRD-0006). People are not here —
  // they project from the prose @-mentions, reconciled on save (RICH-007).
  moods: string[]
  // The single Activity (PRD-0007): a known activity name, 'None', or custom free text. Always present —
  // a Session always has one, defaulting to 'None'. Sent on the full-replace metadata PUT (ADR-0011).
  activity: string
}

/** A directory Person (PRD-0006): the durable id a mention references + its display label. */
export interface Person {
  id: string
  label: string
}

// People left the shared suggestion machinery for their own proposal flow (RICH-009); Topics/Moods remain.
export type SuggestionKind = 'Topic' | 'Mood'

export interface Suggestion {
  kind: SuggestionKind
  // For a Mood, the value is the known mood name or custom text; Topics carry their name.
  value: string
}

/**
 * An AI People-tag proposal awaiting per-Person review (PRD-0006, RICH-009): the proposed name, whether it
 * is new to the directory or auto-links to an existing Person, and every sentence the tag would land in.
 */
export interface PersonTagProposal {
  label: string
  matchedPersonId: string | null
  matchedLabel: string | null
  isNew: boolean
  contexts: string[]
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
  // The Cleaned Revision number of the latest server *regeneration* (Cleanup run or approved People-tag
  // insertion), NOT a user hand-edit. The Cleaned editor keys its remount on this so a regeneration
  // re-seeds it while the user's own debounced saves do not (issue 0028).
  cleanedRegenerationRevisionNumber: number
  topics: string[]
  people: string[]
  moods: string[]
  activity: string
  suggestions: Suggestion[]
  peopleProposals: PersonTagProposal[]
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
  activity: string
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
