import { describe, expect, expectTypeOf, it } from 'vitest'
import { buildSessionFilter, timelineSearchSchema, type TimelineSearch } from './useSessions'

describe('timelineSearchSchema', () => {
  it('parses valid topic/mood/activity', () => {
    expect(timelineSearchSchema.parse({ topic: 'work', mood: 'Calm', activity: 'Walking' })).toEqual({
      topic: 'work',
      mood: 'Calm',
      activity: 'Walking',
    })
  })

  it('falls back to defaults when params are missing', () => {
    expect(timelineSearchSchema.parse({})).toEqual({ topic: '', mood: '', activity: '' })
  })

  it('falls back to defaults when params are the wrong type', () => {
    expect(timelineSearchSchema.parse({ topic: 123, mood: ['nope'], activity: 7 })).toEqual({
      topic: '',
      mood: '',
      activity: '',
    })
  })

  it('normalizes an unknown mood to the empty default but keeps a known one', () => {
    expect(timelineSearchSchema.parse({ mood: 'NotARealMood' }).mood).toBe('')
    expect(timelineSearchSchema.parse({ mood: 'Joyful' }).mood).toBe('Joyful')
  })

  it('normalizes an unknown activity to the empty default but keeps a known one', () => {
    expect(timelineSearchSchema.parse({ activity: 'NotAnActivity' }).activity).toBe('')
    expect(timelineSearchSchema.parse({ activity: 'Walking' }).activity).toBe('Walking')
  })

  it('infers a type matching the schema shape', () => {
    expectTypeOf<TimelineSearch>().toEqualTypeOf<{
      topic: string
      mood:
        | ''
        | 'Joyful'
        | 'Content'
        | 'Calm'
        | 'Neutral'
        | 'Tired'
        | 'Anxious'
        | 'Sad'
        | 'Angry'
        | 'Excited'
        | 'Grateful'
      activity: '' | 'Stationary' | 'Walking' | 'Eating' | 'Commuting' | 'Exercising' | 'Resting'
    }>()
  })
})

describe('buildSessionFilter', () => {
  it('returns undefined when no filters are set', () => {
    expect(buildSessionFilter({ topic: '', mood: '', activity: '' })).toBeUndefined()
  })

  it('ignores a whitespace-only topic', () => {
    expect(buildSessionFilter({ topic: '   ', mood: '', activity: '' })).toBeUndefined()
  })

  it('builds a topic filter (mood/activity are separate params, not part of the QueryKit string)', () => {
    expect(buildSessionFilter({ topic: 'work', mood: 'Calm', activity: 'Walking' })).toBe('topics == "work"')
  })
})
