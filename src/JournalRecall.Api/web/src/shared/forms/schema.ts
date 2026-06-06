/**
 * Shared zod schema fragments (PRD-0004 / ADR-0007). These are the validation rules that are
 * genuinely one decision, so changing the policy is one edit that applies everywhere it should. Pure
 * zod — no React/DOM dependency — imported only by the forms that must change together.
 */

import { z } from 'zod'

/**
 * The server's password policy (ASP.NET Identity, see `AuthRegistration.cs`): minimum length, no
 * character-class requirements. Kept here as the single source the client validates against.
 */
export const PASSWORD_MIN_LENGTH = 10

/** A new-password field validated against the server policy. Reused wherever a password is set. */
export const passwordSchema = z
  .string()
  .min(PASSWORD_MIN_LENGTH, `Password must be at least ${PASSWORD_MIN_LENGTH} characters.`)

/**
 * The username policy — the client mirror of the server's `Username` value object (issue 0027): charset
 * `[a-zA-Z0-9._-]`, length 3–32. The single source the auth forms validate a username against; the
 * server's throwing `Username.Create` is the authority and re-surfaces any miss inline via 422.
 */
export const USERNAME_MIN_LENGTH = 3
export const USERNAME_MAX_LENGTH = 32

export const usernameSchema = z
  .string()
  .trim()
  .min(USERNAME_MIN_LENGTH, `Username must be at least ${USERNAME_MIN_LENGTH} characters.`)
  .max(USERNAME_MAX_LENGTH, `Username must be at most ${USERNAME_MAX_LENGTH} characters.`)
  .regex(/^[a-zA-Z0-9._-]+$/, 'Username may contain only letters, numbers, and . _ -')

/**
 * A `superRefine` that enforces two password fields match, attaching the issue to the confirm field's
 * path so the error renders under it. This is the password-match check that used to be reimplemented
 * in register, setup, and change-password — now expressed once. Field names are parameterized because
 * change-password matches `newPassword` against `confirmPassword`.
 */
export function passwordsMatch(passwordKey = 'password', confirmKey = 'confirmPassword') {
  return (data: Record<string, unknown>, ctx: z.core.$RefinementCtx) => {
    if (data[passwordKey] !== data[confirmKey]) {
      ctx.addIssue({ code: 'custom', message: 'Passwords don’t match.', path: [confirmKey] })
    }
  }
}
