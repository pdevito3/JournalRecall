import { createFileRoute, Link, useNavigate } from '@tanstack/react-router'
import { useMe } from '@/features/auth/useAuth'
import {
  buildSessionFilter,
  sessionListQueryOptions,
  timelineSearchSchema,
  useCreateSession,
} from '@/features/sessions/useSessions'
import { captureLocation } from '@/features/sessions/api'
import { useSettings, useUpdateSettings } from '@/features/settings/useSettings'
import { Timeline } from '@/features/sessions/components/timeline'
import { Button } from '@/shared/ui/button'

export const Route = createFileRoute('/')({
  // Timeline filters live in the URL (FE-009): validated → shareable/bookmark-safe, malformed → defaults.
  validateSearch: timelineSearchSchema,
  // Re-run the loader only when a param that changes the query changes.
  loaderDeps: ({ search: { topic, person, mood } }) => ({ topic, person, mood }),
  // Prime the timeline query for the active filters during navigation (kills the mount→fetch waterfall).
  // The component keeps reading via useQuery, so focus/reconnect refetch, dedup, and GC stay intact.
  loader: ({ context: { queryClient }, deps }) =>
    queryClient.ensureQueryData(sessionListQueryOptions(buildSessionFilter(deps))),
  component: Home,
})

function Home() {
  const { data: user } = useMe()
  const { data: settings } = useSettings()
  const updateSettings = useUpdateSettings()
  const navigate = useNavigate()
  const createSession = useCreateSession()

  async function start() {
    // With geo opt-in on, ask the browser for a point (the user may still decline) before creating.
    const location = settings?.locationCaptureEnabled ? await captureLocation() : undefined
    createSession.mutate(location, {
      onSuccess: (session) =>
        navigate({ to: '/sessions/$sessionId', params: { sessionId: session.id } }),
    })
  }

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
        <Button variant="primary" isDisabled={createSession.isPending} onPress={start}>
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

      {user ? (
        <Timeline settings={settings} onUpdateSettings={(next) => updateSettings.mutate(next)} />
      ) : null}
    </section>
  )
}
