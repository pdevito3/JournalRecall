import { describe, expect, expectTypeOf, it } from 'vitest'
import { buildSessionFilter, timelineSearchSchema, type TimelineSearch } from './useSessions'

describe('timelineSearchSchema', () => {
  it('parses valid topic/person/mood', () => {
    expect(timelineSearchSchema.parse({ topic: 'work', person: 'alex', mood: 'Calm' })).toEqual({
      topic: 'work',
      person: 'alex',
      mood: 'Calm',
    })
  })

  it('falls back to defaults when params are missing', () => {
    expect(timelineSearchSchema.parse({})).toEqual({ topic: '', person: '', mood: '' })
  })

  it('falls back to defaults when params are the wrong type', () => {
    expect(timelineSearchSchema.parse({ topic: 123, person: null, mood: ['nope'] })).toEqual({
      topic: '',
      person: '',
      mood: '',
    })
  })

  it('normalizes an unknown mood to the empty default but keeps a known one', () => {
    expect(timelineSearchSchema.parse({ mood: 'NotARealMood' }).mood).toBe('')
    expect(timelineSearchSchema.parse({ mood: 'Joyful' }).mood).toBe('Joyful')
  })

  it('infers a type matching the schema shape', () => {
    expectTypeOf<TimelineSearch>().toEqualTypeOf<{
      topic: string
      person: string
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
    }>()
  })
})

describe('buildSessionFilter', () => {
  it('returns undefined when no filters are set', () => {
    expect(buildSessionFilter({ topic: '', person: '', mood: '' })).toBeUndefined()
  })

  it('ignores whitespace-only topic/person', () => {
    expect(buildSessionFilter({ topic: '   ', person: ' ', mood: '' })).toBeUndefined()
  })

  it('combines set filters with &&', () => {
    expect(buildSessionFilter({ topic: 'work', person: 'alex', mood: 'Calm' })).toBe(
      'topics == "work" && people == "alex" && mood == "Calm"',
    )
  })
})
