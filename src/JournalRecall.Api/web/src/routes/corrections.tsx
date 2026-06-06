import { createFileRoute } from '@tanstack/react-router'
import { correctionsQueryOptions, CorrectionsPage } from '@/features/corrections'

export const Route = createFileRoute('/corrections')({
  // Prime the corrections list during navigation (kills the mount→fetch waterfall). The component
  // keeps reading via useQuery, so focus/reconnect refetch, dedup, and GC stay intact.
  loader: ({ context: { queryClient } }) => queryClient.ensureQueryData(correctionsQueryOptions()),
  component: CorrectionsPage,
})
