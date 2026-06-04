import { QueryClient } from '@tanstack/react-query'
import { createRouter } from '@tanstack/react-router'
import { routeTree } from './routeTree.gen'

// Single composition root for routing + server-state. The QueryClient is shared with the provider
// in main.tsx and exposed on router context so loaders can prime the cache in later slices.
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: { staleTime: 30_000 },
  },
})

export function getRouter() {
  return createRouter({
    routeTree,
    context: { queryClient },
    basepath: '/app', // the SPA is served under /app (ADR-0001)
    defaultPreload: 'intent',
    scrollRestoration: true,
  })
}

declare module '@tanstack/react-router' {
  interface Register {
    router: ReturnType<typeof getRouter>
  }
}
