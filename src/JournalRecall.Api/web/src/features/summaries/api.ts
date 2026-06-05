export type SummaryPeriod = 'Day' | 'Week'
export type SummaryStatus = 'Missing' | 'Generating' | 'Ready' | 'Stale'

export interface Summary {
  period: SummaryPeriod
  periodDate: string // YYYY-MM-DD anchor (a Day's date, or a Week's ISO Monday)
  status: SummaryStatus
  content: string
  sessionCount: number
  generatedAt: string | null
}

export async function getSummary(period: SummaryPeriod, date: string): Promise<Summary> {
  const res = await fetch(`/api/summaries/${period.toLowerCase()}/${date}`, { credentials: 'include' })
  if (!res.ok) throw new Error('Could not load summary')
  return res.json()
}

export async function generateSummary(period: SummaryPeriod, date: string): Promise<Summary> {
  const res = await fetch(`/api/summaries/${period.toLowerCase()}/${date}/generate`, {
    method: 'POST',
    credentials: 'include',
  })
  if (!res.ok) throw new Error('Could not generate summary')
  return res.json()
}
