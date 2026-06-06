import { QueryClient } from '@tanstack/react-query'
import { createRouter } from '@tanstack/react-router'
import { routeTree } from './routeTree.gen'
import { RouteError, RoutePending } from '@/shared/ui/route-status'

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
    // Let React Query own the cache lifetime of preloaded route data (FE-012). Intent preloading warms
    // loader-backed route data on hover, and each query's own staleTime governs freshness thereafter —
    // 0 here disables the router's separate preload cache so there's no double-fetch on navigation.
    defaultPreloadStaleTime: 0,
    // One consistent loading/failure look for every screen (FE-011). Loader-backed routes read their
    // awaited primary queries via useSuspenseQuery, so a load suspends here and a fetch failure is
    // caught by the default error boundary — no per-component isLoading/isError branches.
    defaultPendingComponent: RoutePending,
    defaultErrorComponent: RouteError,
    scrollRestoration: true,
  })
}

declare module '@tanstack/react-router' {
  interface Register {
    router: ReturnType<typeof getRouter>
  }
}
