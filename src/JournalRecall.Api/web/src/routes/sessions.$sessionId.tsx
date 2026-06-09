import { createFileRoute } from '@tanstack/react-router'
import {
  cleanedRevisionsQueryOptions,
  revisionsQueryOptions,
  sessionQueryOptions,
  SessionEditor,
} from '@/features/sessions'

export const Route = createFileRoute('/sessions/$sessionId')({
  // Await the primary Session (blocks first paint); let the Revision streams prefetch in the
  // background so the editor renders without waiting on history. Components keep reading via useQuery.
  loader: async ({ context: { queryClient }, params: { sessionId } }) => {
    await queryClient.ensureQueryData(sessionQueryOptions(sessionId))
    void queryClient.prefetchQuery(revisionsQueryOptions(sessionId))
    void queryClient.prefetchQuery(cleanedRevisionsQueryOptions(sessionId))
  },
  component: SessionEditorRoute,
})

// Remount the editor per Session: the Router reuses this component across param changes, so a `key`
// on Session identity guarantees a fresh editor (and lets local state seed directly from the server).
function SessionEditorRoute() {
  const { sessionId } = Route.useParams()

  // The session page wants more room than the global reading width (__root's <main> caps every route at
  // max-w-3xl). On lg+ this wrapper breaks out of that cap — a centered, viewport-bounded ~72rem band —
  // so the editor column gets the room it needs while other pages keep their narrow reading width.
  return (
    <div className="lg:relative lg:left-1/2 lg:w-[75rem] lg:max-w-[calc(100vw-2rem)] lg:-translate-x-1/2">
      <SessionEditor key={sessionId} sessionId={sessionId} />
    </div>
  )
}
