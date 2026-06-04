import { createFileRoute, Link, useNavigate } from '@tanstack/react-router'
import { useMe } from '@/features/auth/useAuth'
import { useCreateSession } from '@/features/sessions/useSessions'
import { Timeline } from '@/features/sessions/components/timeline'
import { Button } from '@/shared/ui/button'

export const Route = createFileRoute('/')({
  component: Home,
})

function Home() {
  const { data: user } = useMe()
  const navigate = useNavigate()
  const createSession = useCreateSession()

  return (
    <section className="space-y-6">
      <div className="space-y-2">
        <h1 className="text-2xl font-semibold text-content">Your journal</h1>
        <p className="text-muted">
          Start a session, write or dictate freely, and pick up where you left off. Your raw words stay
          yours — optional AI cleanup arrives in a later slice.
        </p>
      </div>

      {user ? (
        <Button
          variant="primary"
          isDisabled={createSession.isPending}
          onPress={() =>
            createSession.mutate(undefined, {
              onSuccess: (session) =>
                navigate({ to: '/sessions/$sessionId', params: { sessionId: session.id } }),
            })
          }
        >
          {createSession.isPending ? 'Starting…' : 'Start a session'}
        </Button>
      ) : (
        <p className="text-muted">
          <Link to="/login" className="text-accent hover:underline">
            Sign in
          </Link>{' '}
          to start journaling.
        </p>
      )}

      {createSession.isError ? (
        <p className="text-sm text-red-400">{createSession.error.message}</p>
      ) : null}

      {user ? <Timeline /> : null}
    </section>
  )
}
