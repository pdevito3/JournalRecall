// Public API for the settings feature. `useSettings` is entirely cross-boundary surface
// (hooks + queryOptions). From `api.ts` only the `UserSettings` type is exposed; fetch fns are internal.
export * from './useSettings'
export type { UserSettings } from './api'
