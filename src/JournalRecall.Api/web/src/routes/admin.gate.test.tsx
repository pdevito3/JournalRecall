import { describe, expect, it } from 'vitest'
import { QueryClient } from '@tanstack/react-query'
import { isRedirect } from '@tanstack/react-router'
import { ensureAdmin } from './admin'
import { authKeys } from '@/features/auth'
import type { AuthUser } from '@/features/auth/api'

/**
 * FE-006: the Admin role gate runs at navigation time (route `beforeLoad`), not in the component, so a
 * non-admin never sees a flash of the Admin surface. The .NET functional harness can't exercise a
 * client-side TanStack Router redirect, so this satisfies the acceptance intent at the layer that owns
 * the rule: it drives `ensureAdmin` over the representative `me` payloads (admin / member / signed-out)
 * primed into the same `['me']` cache the route reads, asserting the shared `selectIsAdmin` rule admits
 * an Admin and redirects everyone else to the journal (`/`).
 */
const admin: AuthUser = { id: '1', username: 'root', roles: ['Admin', 'Member'], mustChangePassword: false }
const member: AuthUser = { id: '2', username: 'jo', roles: ['Member'], mustChangePassword: false }

function clientWithMe(me: AuthUser | null) {
  const queryClient = new QueryClient()
  queryClient.setQueryData(authKeys.me, me)
  return queryClient
}

describe('Admin route beforeLoad gate (ensureAdmin)', () => {
  it('admits an Admin (no redirect thrown)', async () => {
    await expect(ensureAdmin(clientWithMe(admin))).resolves.toBeUndefined()
  })

  it('redirects a non-Admin Member to the journal', async () => {
    await expect(ensureAdmin(clientWithMe(member))).rejects.toSatisfy(
      (e) => isRedirect(e) && (e as { options?: { to?: string } }).options?.to === '/',
    )
  })

  it('redirects a signed-out user (null me) to the journal', async () => {
    await expect(ensureAdmin(clientWithMe(null))).rejects.toSatisfy(
      (e) => isRedirect(e) && (e as { options?: { to?: string } }).options?.to === '/',
    )
  })
})
