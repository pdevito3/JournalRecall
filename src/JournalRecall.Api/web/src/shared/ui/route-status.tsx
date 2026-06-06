import type { ErrorComponentProps } from '@tanstack/react-router'

// Router-level defaults (FE-011): one loading look and one failure look for every screen. Loader-backed
// routes read their awaited primary queries via useSuspenseQuery, so a load suspends to RoutePending and
// a fetch failure throws to RouteError — no bespoke per-component isLoading/isError branches.

export function RoutePending() {
  return <p className="text-muted">Loading…</p>
}

export function RouteError({ error }: ErrorComponentProps) {
  const message = error instanceof Error ? error.message : 'Something went wrong.'
  return <p className="text-muted">{message}</p>
}
