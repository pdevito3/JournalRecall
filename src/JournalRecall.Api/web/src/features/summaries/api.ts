export type SummaryPeriod = 'Day' | 'Week' | 'Month' | 'Quarter' | 'Year'
export type SummaryStatus = 'Missing' | 'Generating' | 'Ready' | 'Stale'

export const PERIODS: SummaryPeriod[] = ['Day', 'Week', 'Month', 'Quarter', 'Year']

export interface Summary {
  period: SummaryPeriod
  periodDate: string // YYYY-MM-DD anchor (a Day's date, a Week's ISO Monday, else the period's first day)
  status: SummaryStatus
  content: string
  sourceCount: number // Sessions for a Day/Week, lower-level Summaries for a roll-up
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
