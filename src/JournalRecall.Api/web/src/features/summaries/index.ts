// Public API for the summaries feature. `useSummaries` is entirely cross-boundary surface
// (hooks + queryOptions + URL-search schema). From `api.ts` only the call-site types/constants are
// exposed; the fetch fns are internal.
export * from './useSummaries'
export { PERIODS } from './api'
export type { Summary, SummaryPeriod, SummaryStatus } from './api'
