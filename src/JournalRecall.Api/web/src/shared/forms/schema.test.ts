import { describe, expect, it } from 'vitest'
import { z } from 'zod'
import {
  PASSWORD_MIN_LENGTH,
  passwordSchema,
  passwordsMatch,
  usernameSchema,
  USERNAME_MAX_LENGTH,
  USERNAME_MIN_LENGTH,
} from './schema'

describe('passwordSchema', () => {
  it('rejects a password shorter than the policy length', () => {
    expect(passwordSchema.safeParse('a'.repeat(PASSWORD_MIN_LENGTH - 1)).success).toBe(false)
  })

  it('accepts a password at the policy boundary and longer', () => {
    expect(passwordSchema.safeParse('a'.repeat(PASSWORD_MIN_LENGTH)).success).toBe(true)
    expect(passwordSchema.safeParse('a'.repeat(PASSWORD_MIN_LENGTH + 5)).success).toBe(true)
  })
})

describe('usernameSchema', () => {
  it.each(['abc', 'name.surname', 'a_b-c.9', 'A'.repeat(USERNAME_MAX_LENGTH)])('accepts %s', (value) => {
    expect(usernameSchema.safeParse(value).success).toBe(true)
  })

  it.each([
    '',
    'ab', // shorter than min
    'A'.repeat(USERNAME_MAX_LENGTH + 1),
    'has space',
    'name@example.com', // @ is no longer allowed
    'bad!char',
  ])('rejects %s', (value) => {
    expect(usernameSchema.safeParse(value).success).toBe(false)
  })

  it('enforces the documented length boundaries', () => {
    expect(usernameSchema.safeParse('a'.repeat(USERNAME_MIN_LENGTH)).success).toBe(true)
    expect(usernameSchema.safeParse('a'.repeat(USERNAME_MIN_LENGTH - 1)).success).toBe(false)
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
