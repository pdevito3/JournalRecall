import type { ReactNode } from 'react'
import { createRootRouteWithContext, Link, Outlet, useNavigate } from '@tanstack/react-router'
import type { QueryClient } from '@tanstack/react-query'
import { useLogout, useMe } from '@/features/auth/useAuth'
import { Button } from '@/shared/ui/button'

export interface RouterContext {
  queryClient: QueryClient
}

export const Route = createRootRouteWithContext<RouterContext>()({
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
          <NavLink to="/chat">Chat</NavLink>
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
