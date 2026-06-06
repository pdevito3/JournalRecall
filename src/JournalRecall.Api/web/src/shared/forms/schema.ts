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

/** An email field. Shared by the auth forms that all enter an email. */
export const emailSchema = z.email('Enter a valid email address.')

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
