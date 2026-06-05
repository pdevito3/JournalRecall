import { useEffect, useMemo, useState } from 'react'
import { createFileRoute } from '@tanstack/react-router'
import { useMe } from '@/features/auth/useAuth'
import { useGenerateSummary, useSummary } from '@/features/summaries/useSummaries'
import type { SummaryPeriod } from '@/features/summaries/api'
import { Button } from '@/shared/ui/button'

export const Route = createFileRoute('/summaries')({
  component: Summaries,
})

function todayYmd(): string {
  const now = new Date()
  return ymd(now.getFullYear(), now.getMonth() + 1, now.getDate())
}

function ymd(y: number, m: number, d: number): string {
  return `${y}-${String(m).padStart(2, '0')}-${String(d).padStart(2, '0')}`
}

/** The ISO-8601 Monday (week anchor) for a YYYY-MM-DD date, so any day in a week maps to one Summary. */
function isoMonday(date: string): string {
  const [y, m, d] = date.split('-').map(Number)
  const dt = new Date(y!, m! - 1, d!)
  const back = (dt.getDay() + 6) % 7 // days since Monday
  dt.setDate(dt.getDate() - back)
  return ymd(dt.getFullYear(), dt.getMonth() + 1, dt.getDate())
}

function Summaries() {
  const { data: user } = useMe()
  const [period, setPeriod] = useState<SummaryPeriod>('Day')
  const [picked, setPicked] = useState(todayYmd)

  // For a Week, the canonical key/param is the ISO Monday; a Day is its own date.
  const date = period === 'Week' ? isoMonday(picked) : picked

  if (!user) {
    return <p className="text-muted">Sign in to see your summaries.</p>
  }

  return (
    <section className="space-y-6">
      <div className="space-y-2">
        <h1 className="text-2xl font-semibold text-content">Summaries</h1>
        <p className="text-muted">
          An AI recap of a day or a week, built from your sessions — reading the cleaned copy when one
          exists, else your raw words. Generated on demand; nothing runs in the background.
        </p>
      </div>

      <div className="flex flex-wrap items-center gap-4">
        <div className="inline-flex rounded-lg border border-border bg-surface-2 p-0.5">
          {(['Day', 'Week'] as const).map((p) => (
            <button
              key={p}
              type="button"
              onClick={() => setPeriod(p)}
              className={`rounded-md px-3 py-1 text-sm transition-colors ${
                period === p ? 'bg-surface-3 text-content' : 'text-muted hover:text-content'
              }`}
            >
              {p}
            </button>
          ))}
        </div>
        <label className="flex items-center gap-2 text-sm text-muted">
          {period === 'Week' ? 'Week of' : 'Day'}
          <input
            type="date"
            value={picked}
            onChange={(e) => setPicked(e.target.value || todayYmd())}
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
  const hasSessions = (summary?.sessionCount ?? 0) > 0

  // Open-the-page generation: when a period has sessions but no Summary yet, kick one off (issue 0013).
  const shouldAutoGenerate =
    summary?.status === 'Missing' && hasSessions && !generate.isPending && !generate.isSuccess
  useEffect(() => {
    if (shouldAutoGenerate) generate.mutate()
  }, [shouldAutoGenerate, generate])

  const rangeLabel = useMemo(() => formatRange(period, date), [period, date])

  if (isLoading) return <p className="text-muted">Loading…</p>

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h2 className="font-medium text-content">{rangeLabel}</h2>
          {summary ? (
            <p className="text-xs text-muted">
              {summary.sessionCount} {summary.sessionCount === 1 ? 'session' : 'sessions'}
              {summary.generatedAt
                ? ` · generated ${new Date(summary.generatedAt).toLocaleString()}`
                : ''}
            </p>
          ) : null}
        </div>
        {hasSessions ? (
          <Button isDisabled={generating} onPress={() => generate.mutate()}>
            {generating ? 'Generating…' : summary?.status === 'Ready' ? 'Refresh' : 'Generate'}
          </Button>
        ) : null}
      </div>

      {generate.isError ? (
        <p className="text-sm text-red-400">Could not generate this summary. Try again.</p>
      ) : null}

      {generating ? (
        <p className="text-muted">Generating your summary…</p>
      ) : !hasSessions ? (
        <p className="text-muted">No sessions in this {period.toLowerCase()} to summarize.</p>
      ) : summary?.status === 'Ready' && summary.content ? (
        <article className="whitespace-pre-wrap rounded-lg border border-border bg-surface-2 p-4 text-content">
          {summary.content}
        </article>
      ) : (
        <p className="text-muted">No summary yet.</p>
      )}
    </div>
  )
}

function formatRange(period: SummaryPeriod, date: string): string {
  const [y, m, d] = date.split('-').map(Number)
  const start = new Date(y!, m! - 1, d!)
  const opts: Intl.DateTimeFormatOptions = { year: 'numeric', month: 'long', day: 'numeric' }
  if (period === 'Day') {
    return start.toLocaleDateString(undefined, { weekday: 'long', ...opts })
  }
  const end = new Date(start)
  end.setDate(start.getDate() + 6)
  return `${start.toLocaleDateString(undefined, opts)} – ${end.toLocaleDateString(undefined, opts)}`
}
