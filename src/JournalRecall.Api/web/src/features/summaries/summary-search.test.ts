import { describe, expect, expectTypeOf, it } from 'vitest'
import { summarySearchSchema, todayYmd, type SummarySearch } from './useSummaries'

describe('summarySearchSchema', () => {
  it('parses a valid period + date', () => {
    expect(summarySearchSchema.parse({ period: 'Week', date: '2026-03-15' })).toEqual({
      period: 'Week',
      date: '2026-03-15',
    })
  })

  it('falls back to defaults when params are missing', () => {
    expect(summarySearchSchema.parse({})).toEqual({ period: 'Day', date: todayYmd() })
  })

  it('falls back to defaults when params are the wrong type', () => {
    expect(summarySearchSchema.parse({ period: 123, date: null })).toEqual({
      period: 'Day',
      date: todayYmd(),
    })
  })

  it('normalizes an unknown period to the default but keeps a known one', () => {
    expect(summarySearchSchema.parse({ period: 'Decade' }).period).toBe('Day')
    expect(summarySearchSchema.parse({ period: 'Quarter' }).period).toBe('Quarter')
  })

  it('normalizes a malformed date to today but keeps a YYYY-MM-DD one', () => {
    expect(summarySearchSchema.parse({ date: 'not-a-date' }).date).toBe(todayYmd())
    expect(summarySearchSchema.parse({ date: '2026-03-15' }).date).toBe('2026-03-15')
  })

  it('infers a type matching the schema shape', () => {
    expectTypeOf<SummarySearch>().toEqualTypeOf<{
      period: 'Day' | 'Week' | 'Month' | 'Quarter' | 'Year'
      date: string
    }>()
  })
})
