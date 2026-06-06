import { describe, expect, it } from 'vitest'
import { z } from 'zod'
import { emailSchema, PASSWORD_MIN_LENGTH, passwordSchema, passwordsMatch } from './schema'

describe('passwordSchema', () => {
  it('rejects a password shorter than the policy length', () => {
    expect(passwordSchema.safeParse('a'.repeat(PASSWORD_MIN_LENGTH - 1)).success).toBe(false)
  })

  it('accepts a password at the policy boundary and longer', () => {
    expect(passwordSchema.safeParse('a'.repeat(PASSWORD_MIN_LENGTH)).success).toBe(true)
    expect(passwordSchema.safeParse('a'.repeat(PASSWORD_MIN_LENGTH + 5)).success).toBe(true)
  })
})

describe('emailSchema', () => {
  it.each(['a@b.co', 'name.surname@example.com'])('accepts %s', (value) => {
    expect(emailSchema.safeParse(value).success).toBe(true)
  })

  it.each(['', 'not-an-email', 'foo@', '@bar.com'])('rejects %s', (value) => {
    expect(emailSchema.safeParse(value).success).toBe(false)
  })
})

describe('passwordsMatch', () => {
  const schema = z
    .object({ password: z.string(), confirmPassword: z.string() })
    .superRefine(passwordsMatch())

  it('passes when both fields are equal', () => {
    expect(schema.safeParse({ password: 'corghorse42', confirmPassword: 'corghorse42' }).success).toBe(true)
  })

  it('fails with the issue attached to the confirm field when they differ', () => {
    const result = schema.safeParse({ password: 'corghorse42', confirmPassword: 'mismatch99' })
    expect(result.success).toBe(false)
    if (!result.success) {
      expect(result.error.issues).toHaveLength(1)
      expect(result.error.issues[0]!.path).toEqual(['confirmPassword'])
      expect(result.error.issues[0]!.message).toMatch(/match/i)
    }
  })

  it('supports custom field names (newPassword/confirmPassword)', () => {
    const changeSchema = z
      .object({ newPassword: z.string(), confirmPassword: z.string() })
      .superRefine(passwordsMatch('newPassword'))
    expect(changeSchema.safeParse({ newPassword: 'a', confirmPassword: 'b' }).success).toBe(false)
    expect(changeSchema.safeParse({ newPassword: 'same', confirmPassword: 'same' }).success).toBe(true)
  })
})
