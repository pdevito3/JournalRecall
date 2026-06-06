import { useEffect, useMemo } from 'react'
import { createFileRoute } from '@tanstack/react-router'
import { useMe } from '@/features/auth/useAuth'
import {
  summaryQueryOptions,
  summarySearchSchema,
  todayYmd,
  useGenerateSummary,
  useSummary,
} from '@/features/summaries/useSummaries'
import { PERIODS, type SummaryPeriod } from '@/features/summaries/api'
import { Button } from '@/shared/ui/button'

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
  const { data: user } = useMe()
  const { period, date: picked } = Route.useSearch()
  const navigate = Route.useNavigate()

  const date = anchorFor(period, picked)

  if (!user) {
    return <p className="text-muted">Sign in to see your summaries.</p>
  }

  return (
    <section className="space-y-6">
      <div className="space-y-2">
        <h1 className="text-2xl font-semibold text-content">Summaries</h1>
        <p className="text-muted">
          An AI recap over a period — a day or week is built from your sessions (the cleaned copy when one
          exists, else your raw words); a month rolls up its days, a quarter its months, a year its
          quarters. Generated on demand; nothing runs in the background.
        </p>
      </div>

      <div className="flex flex-wrap items-center gap-4">
        <div className="inline-flex rounded-lg border border-border bg-surface-2 p-0.5">
          {PERIODS.map((p) => (
            <button
              key={p}
              type="button"
              onClick={() => navigate({ search: (prev) => ({ ...prev, period: p }) })}
              className={`rounded-md px-3 py-1 text-sm transition-colors ${
                period === p ? 'bg-surface-3 text-content' : 'text-muted hover:text-content'
              }`}
            >
              {p}
            </button>
          ))}
        </div>
        <label className="flex items-center gap-2 text-sm text-muted">
          {period === 'Day' ? 'Day' : `Pick a day in the ${period.toLowerCase()}`}
          <input
            type="date"
            value={picked}
            onChange={(e) =>
              navigate({ search: (prev) => ({ ...prev, date: e.target.value || todayYmd() }) })
            }
            className="rounded-lg border border-border bg-surface-2 px-2 py-1 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
          />
        </label>
      </div>

      <SummaryView period={period} date={date} />
    </section>
  )
}

function SummaryView({ period, date }: { period: SummaryPeriod; date: string }) {
  const { data: summary, isLoading } = useSummary(period, date)
  const generate = useGenerateSummary(period, date)

  const generating = generate.isPending || summary?.status === 'Generating'
  const hasSources = (summary?.sourceCount ?? 0) > 0
  const isStale = summary?.status === 'Stale'

  // Open-the-page generation: when a period has source material but no Summary yet, kick one off. A
  // Stale Summary is *not* auto-regenerated — it offers regeneration instead (issue 0014).
  const shouldAutoGenerate =
    summary?.status === 'Missing' && hasSources && !generate.isPending && !generate.isSuccess
  useEffect(() => {
    if (shouldAutoGenerate) generate.mutate()
  }, [shouldAutoGenerate, generate])

  const rangeLabel = useMemo(() => formatRange(period, date), [period, date])
  const buttonLabel = generating ? 'Generating…' : isStale ? 'Regenerate' : summary?.content ? 'Refresh' : 'Generate'

  if (isLoading) return <p className="text-muted">Loading…</p>

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h2 className="font-medium text-content">{rangeLabel}</h2>
          {summary ? (
            <p className="text-xs text-muted">
              {summary.sourceCount} {sourceNoun(period, summary.sourceCount)}
              {summary.generatedAt ? ` · generated ${new Date(summary.generatedAt).toLocaleString()}` : ''}
            </p>
          ) : null}
        </div>
        {hasSources ? (
          <Button isDisabled={generating} onPress={() => generate.mutate()}>
            {buttonLabel}
          </Button>
        ) : null}
      </div>

      {isStale && !generating ? (
        <p className="rounded-lg border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-sm text-amber-300">
          Something in this {period.toLowerCase()} changed since this summary was generated — regenerate to
          bring it up to date.
        </p>
      ) : null}

      {generate.isError ? (
        <p className="text-sm text-red-400">Could not generate this summary. Try again.</p>
      ) : null}

      {generating ? (
        <p className="text-muted">Generating your summary…</p>
      ) : !hasSources ? (
        <p className="text-muted">{emptyMessage(period)}</p>
      ) : summary?.content ? (
        <article className="whitespace-pre-wrap rounded-lg border border-border bg-surface-2 p-4 text-content">
          {summary.content}
        </article>
      ) : (
        <p className="text-muted">No summary yet.</p>
      )}
    </div>
  )
}

function sourceNoun(period: SummaryPeriod, count: number): string {
  const child =
    period === 'Day' || period === 'Week'
      ? 'session'
      : period === 'Month'
        ? 'day summary'
        : period === 'Quarter'
          ? 'month summary'
          : 'quarter summary'
  return count === 1 ? child : `${child}s`
}

function emptyMessage(period: SummaryPeriod): string {
  if (period === 'Day' || period === 'Week') {
    return `No sessions in this ${period.toLowerCase()} to summarize.`
  }
  return `No ${sourceNoun(period, 2)} yet to roll up — generate the level below first.`
}

function formatRange(period: SummaryPeriod, date: string): string {
  const [y, m, d] = date.split('-').map(Number)
  const start = new Date(y!, m! - 1, d!)
  const monthDay: Intl.DateTimeFormatOptions = { year: 'numeric', month: 'long', day: 'numeric' }

  switch (period) {
    case 'Day':
      return start.toLocaleDateString(undefined, { weekday: 'long', ...monthDay })
    case 'Week': {
      const end = new Date(start)
      end.setDate(start.getDate() + 6)
      return `${start.toLocaleDateString(undefined, monthDay)} – ${end.toLocaleDateString(undefined, monthDay)}`
    }
    case 'Month':
      return start.toLocaleDateString(undefined, { year: 'numeric', month: 'long' })
    case 'Quarter':
      return `Q${Math.floor((m! - 1) / 3) + 1} ${y}`
    case 'Year':
      return String(y)
  }
}
