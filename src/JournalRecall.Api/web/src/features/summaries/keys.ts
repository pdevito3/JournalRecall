import type { SummaryPeriod } from './api'

// Query-key factory for the summaries feature (FE-031). `detail(period, date)` is the per-roll-up read;
// it sits under the `summary` root so a future blanket invalidate could cascade across roll-ups.
export const summaryKeys = {
  all: ['summary'] as const,
  detail: (period: SummaryPeriod, date: string) => [...summaryKeys.all, period, date] as const,
}
