// Public API for the corrections feature. `useCorrections` is entirely cross-boundary surface
// (hooks + queryOptions). From `api.ts` only the `Correction` type is exposed; fetch fns are internal.
export * from './useCorrections'
export { CorrectionsPage } from './components/corrections-page'
export type { Correction } from './api'
