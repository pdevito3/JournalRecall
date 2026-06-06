/**
 * The one shared helper that maps a caught server error onto a react-form instance (PRD-0004 /
 * ADR-0007). Wired identically into every form's `onError`, so server-error handling is written once.
 *
 * A `ProblemError` (FORM-002) carrying a per-field `errors` dict has each entry routed to the matching
 * form field (server PascalCase → camelCase field name); unmatched keys, a bare `title`/`detail`, or
 * any non-`ProblemError` go to a single form-level banner. Everything is written in one `onServer`
 * pass so the field components and `FormShell` surface it.
 */

import type { AnyFormApi } from '@tanstack/react-form'
import { ProblemError } from '@/shared/api/problem'

const GENERIC_MESSAGE = 'Something went wrong. Please try again.'

export function applyServerErrors(form: AnyFormApi, error: unknown): void {
  const fields: Record<string, string> = {}
  let banner: string | undefined

  if (error instanceof ProblemError && error.problem?.errors) {
    const known = new Set(Object.keys((form.state.values as Record<string, unknown>) ?? {}))
    const unmatched: string[] = []
    for (const [serverKey, messages] of Object.entries(error.problem.errors)) {
      const message = (messages ?? []).filter(Boolean).join(' ')
      if (!message) continue
      const fieldName = toFieldName(serverKey)
      if (known.has(fieldName)) fields[fieldName] = message
      else unmatched.push(message)
    }
    // Only show a banner for errors that didn't land on a field — field errors speak for themselves.
    banner = unmatched.length > 0 ? unmatched.join(' ') : undefined
  } else {
    banner = messageOf(error)
  }

  form.setErrorMap({ onServer: { form: banner, fields } } as never)
}

/** Server field names are PascalCase (and sometimes dotted or `$.`-prefixed); fields are camelCase. */
function toFieldName(serverKey: string): string {
  const leaf = serverKey.split('.').pop() || serverKey
  return leaf.charAt(0).toLowerCase() + leaf.slice(1)
}

function messageOf(error: unknown): string {
  if (error instanceof Error && error.message) return error.message
  if (typeof error === 'string' && error) return error
  return GENERIC_MESSAGE
}
