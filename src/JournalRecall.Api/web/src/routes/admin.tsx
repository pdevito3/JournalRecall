import { createFileRoute, redirect } from '@tanstack/react-router'
import type { QueryClient } from '@tanstack/react-query'
import { meQueryOptions, selectIsAdmin } from '@/features/auth'
import {
  adminUsersQueryOptions,
  aiProviderQueryOptions,
  registrationSettingsQueryOptions,
  AdminPage,
} from '@/features/admin'

/**
 * Navigation-time Admin gate: ensure the `me` cache (the root guard already requires a session here)
 * and bounce non-admins to the journal before the Admin page renders — no flash of the wrong surface.
 * Routes through the shared {@link selectIsAdmin} rule (FE-003) so this gate and the nav can't drift.
 */
export async function ensureAdmin(queryClient: QueryClient): Promise<void> {
  const me = await queryClient.ensureQueryData(meQueryOptions())
  if (!selectIsAdmin(me)) throw redirect({ to: '/' })
}

export const Route = createFileRoute('/admin')({
  beforeLoad: ({ context }) => ensureAdmin(context.queryClient),
  // Prime the page's first-paint queries during navigation (kills the mount→fetch waterfall).
  // Components keep reading via useQuery, so focus/reconnect refetch, dedup, and GC stay intact.
  loader: ({ context: { queryClient } }) =>
    Promise.all([
      queryClient.ensureQueryData(adminUsersQueryOptions()),
      queryClient.ensureQueryData(registrationSettingsQueryOptions()),
      queryClient.ensureQueryData(aiProviderQueryOptions()),
    ]),
  component: AdminPage,
})
