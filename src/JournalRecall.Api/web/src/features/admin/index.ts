// Public API for the admin feature. `useAdmin` is entirely cross-boundary surface (hooks +
// queryOptions). From `api.ts` only the call-site types/constants are exposed; the fetch fns are internal.
export * from './useAdmin'
export { AdminPage } from './components/admin-page'
export { PROVIDERS, ROLES } from './api'
export type { AdminUser, AiProvider } from './api'
