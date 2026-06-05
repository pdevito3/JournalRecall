import type { ReactNode } from 'react'
import { createRootRouteWithContext, Link, Outlet, redirect, useNavigate } from '@tanstack/react-router'
import type { QueryClient } from '@tanstack/react-query'
import { fetchAuthConfig, fetchMe } from '@/features/auth/api'
import { useLogout, useMe } from '@/features/auth/useAuth'
import { Button } from '@/shared/ui/button'

export interface RouterContext {
  queryClient: QueryClient
}

// Anonymous client routes that don't require a session (mirrors the server access gate, issue 0022).
const PUBLIC_ROUTES = ['/login', '/register', '/setup']

export const Route = createRootRouteWithContext<RouterContext>()({
  // Client-side access guard: instant in-app redirects that mirror the server gate, so navigation
  // doesn't need a round-trip. The server gate still backstops cold loads / deep-links.
  beforeLoad: async ({ context, location }) => {
    const path = location.pathname
    const config = await context.queryClient.ensureQueryData({
      queryKey: ['auth', 'config'],
      queryFn: fetchAuthConfig,
      staleTime: 30_000,
    })

    // Fresh instance: funnel everyone to setup until the root Admin exists.
    if (config.needsSetup) {
      if (path !== '/setup') throw redirect({ to: '/setup' })
      return
    }
    // Already set up: setup is closed; public auth routes stay open.
    if (path === '/setup') throw redirect({ to: '/login' })
    if (PUBLIC_ROUTES.includes(path)) return

    // Protected route: require a session (fetchMe silently refreshes an expired access token).
    const me = await context.queryClient.ensureQueryData({ queryKey: ['me'], queryFn: fetchMe })
    if (!me) throw redirect({ to: '/login' })
  },
  component: RootLayout,
})

function RootLayout() {
  return (
    <div className="flex min-h-screen flex-col">
      <header className="border-b border-border">
        <nav className="mx-auto flex h-14 max-w-3xl items-center gap-1 px-4">
          <span className="mr-4 font-semibold text-content">JournalRecall</span>
          <NavLink to="/" exact>
            Journal
          </NavLink>
          <NavLink to="/summaries">Summaries</NavLink>
          <NavLink to="/corrections">Corrections</NavLink>
          <NavLink to="/chat">Chat</NavLink>
          <AdminNavLink />
          <div className="ml-auto flex items-center gap-2">
            <AuthControls />
          </div>
        </nav>
      </header>
      <main className="mx-auto w-full max-w-3xl flex-1 px-4 py-10">
        <Outlet />
      </main>
    </div>
  )
}

/** The admin surface link, shown only to users with the Admin role. */
function AdminNavLink() {
  const { data: user } = useMe()
  if (!user?.roles?.includes('Admin')) return null
  return <NavLink to="/admin">Admin</NavLink>
}

function AuthControls() {
  const { data: user, isLoading } = useMe()
  const logout = useLogout()
  const navigate = useNavigate()

  if (isLoading) return null

  if (!user) {
    return (
      <>
        <NavLink to="/login">Sign in</NavLink>
        <NavLink to="/register">Register</NavLink>
      </>
    )
  }

  return (
    <>
      <span className="text-sm text-muted">{user.email}</span>
      <Button onPress={() => logout.mutate(undefined, { onSuccess: () => navigate({ to: '/login' }) })}>
        Sign out
      </Button>
    </>
  )
}

function NavLink({ to, exact, children }: { to: string; exact?: boolean; children: ReactNode }) {
  return (
    <Link
      to={to}
      activeOptions={{ exact: exact ?? false }}
      className="inline-flex h-9 items-center rounded-lg px-3 text-sm text-muted transition-colors hover:bg-surface-3 hover:text-content [&.active]:bg-surface-3 [&.active]:text-content"
    >
      {children}
    </Link>
  )
}
