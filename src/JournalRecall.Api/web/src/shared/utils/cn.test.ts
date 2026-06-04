import { describe, expect, it } from 'vitest'
import { cn } from './cn'

describe('cn', () => {
  it('merges conditional classes', () => {
    expect(cn('a', false && 'b', 'c')).toBe('a c')
  })

  it('lets later Tailwind utilities win conflicts', () => {
    expect(cn('px-2 text-muted', 'px-4')).toBe('text-muted px-4')
  })
})
