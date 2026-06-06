import { createFileRoute } from '@tanstack/react-router'
import {
  summaryQueryOptions,
  summarySearchSchema,
  SummariesPage,
  type SummaryPeriod,
} from '@/features/summaries'

export const Route = createFileRoute('/summaries')({
  // Summary period + anchor date live in the URL (FE-010): validated → shareable/refresh-safe,
  // malformed → defaults.
  validateSearch: summarySearchSchema,
  // Re-run the loader only when the period or picked date changes.
  loaderDeps: ({ search: { period, date } }) => ({ period, date }),
  // Prime the summary query for the active period/date during navigation (kills the mount→fetch
  // waterfall). The component keeps reading via useQuery, so focus/reconnect refetch stays intact.
  loader: ({ context: { queryClient }, deps: { period, date } }) =>
    queryClient.ensureQueryData(summaryQueryOptions(period, anchorFor(period, date))),
  component: Summaries,
})

function ymd(y: number, m: number, d: number): string {
  return `${y}-${String(m).padStart(2, '0')}-${String(d).padStart(2, '0')}`
}

/** The canonical anchor (matches the server): a Day is itself; a Week is its ISO Monday; the rest is the
 *  first calendar day. Normalizing client-side keeps one cache key + one Summary per period. */
function anchorFor(period: SummaryPeriod, date: string): string {
  const [y, m, d] = date.split('-').map(Number)
  switch (period) {
    case 'Day':
      return date
    case 'Week': {
      const dt = new Date(y!, m! - 1, d!)
      dt.setDate(dt.getDate() - ((dt.getDay() + 6) % 7)) // back up to Monday
      return ymd(dt.getFullYear(), dt.getMonth() + 1, dt.getDate())
    }
    case 'Month':
      return ymd(y!, m!, 1)
    case 'Quarter':
      return ymd(y!, Math.floor((m! - 1) / 3) * 3 + 1, 1)
    case 'Year':
      return ymd(y!, 1, 1)
  }
}

function Summaries() {
  const { period, date } = Route.useSearch()
  const navigate = Route.useNavigate()

  return (
    <SummariesPage
      period={period}
      date={date}
      onChange={(next) => navigate({ search: (prev) => ({ ...prev, ...next }) })}
    />
  )
}
