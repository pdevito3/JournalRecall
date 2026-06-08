// Query-key factory for the sessions feature (FE-031), following the tkdodo "effective query keys"
// pattern (https://tkdodo.eu/blog/effective-react-query-keys): a generic→specific hierarchy rooted at
// one `all` key, every level built from the one above. Because `detail(id)` is a prefix of its
// `revisions(id)`/`cleanedRevisions(id)` streams, a single `invalidateQueries(sessionKeys.detail(id))`
// cascades to that session's revision history; `invalidateQueries(sessionKeys.all)` cascades to lists,
// details, and every revision stream.
//
// `topics` and `people` are genuinely separate cache roots (directory references, not per-session), so
// they live here as their own constants rather than under the `sessions` prefix.
export const sessionKeys = {
  all: ['sessions'] as const,
  lists: () => [...sessionKeys.all, 'list'] as const,
  list: (params: { filter?: string; mood?: string; activity?: string }) => [...sessionKeys.lists(), params] as const,
  details: () => [...sessionKeys.all, 'detail'] as const,
  detail: (id: string) => [...sessionKeys.details(), id] as const,
  revisions: (id: string) => [...sessionKeys.detail(id), 'revisions'] as const,
  revision: (id: string, n: number | null) => [...sessionKeys.revisions(id), n] as const,
  cleanedRevisions: (id: string) => [...sessionKeys.detail(id), 'cleaned-revisions'] as const,
  cleanedRevision: (id: string, n: number | null) => [...sessionKeys.cleanedRevisions(id), n] as const,
  topics: ['topics'] as const,
  people: ['people'] as const,
}
