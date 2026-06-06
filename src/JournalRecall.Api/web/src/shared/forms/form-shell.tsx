/**
 * The chrome every form repeats (PRD-0004 / ADR-0007): an optional title, the fields (children), a
 * top-level error banner fed by the form's form-level errors (where `applyServerErrors` routes
 * non-field errors), an optional footer, and a submit button gated on `canSubmit`/`isSubmitting`.
 * Presentational only — each form owns its own `useForm`, schema, and submit.
 */

import type { FormEvent, ReactNode } from 'react'
import type { ReactFormExtendedApi } from '@tanstack/react-form'
import { Button } from '@/shared/ui/button'

/** Any form returned by `useForm` — FormShell only reads state and submits, so the shapes don't matter. */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type AnyReactForm = ReactFormExtendedApi<any, any, any, any, any, any, any, any, any, any, any, any>

interface FormShellProps {
  form: AnyReactForm
  submitLabel: string
  title?: ReactNode
  /** Rendered on the submit button while the form is submitting. */
  pendingLabel?: string
  footer?: ReactNode
  /** The fields, typically `form.Field` render-props composing the bound field components. */
  children: ReactNode
  className?: string
}

function messagesOf(errors: readonly unknown[]): string[] {
  return errors
    .map((error) => {
      if (typeof error === 'string') return error
      if (error && typeof error === 'object' && 'message' in error) return String((error as { message: unknown }).message)
      // Anything else — notably the standard-schema aggregate object react-form puts in form-level
      // `state.errors` while a field is invalid — isn't a user-facing message; drop it rather than
      // render "[object Object]" in the banner.
      return ''
    })
    .filter((message) => message.length > 0)
}

export function FormShell({ form, submitLabel, title, pendingLabel = 'Working…', footer, children, className }: FormShellProps) {
  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    event.stopPropagation()
    void form.handleSubmit()
  }

  return (
    <section className={className ?? 'mx-auto max-w-sm space-y-6'}>
      {title ? <h1 className="text-2xl font-semibold text-content">{title}</h1> : null}
      <form onSubmit={handleSubmit} className="space-y-4" noValidate>
        {children}

        <form.Subscribe selector={(state) => state.errors}>
          {(errors) => {
            const banner = messagesOf(errors).join(' ')
            return banner ? (
              <p role="alert" className="text-sm text-red-400">
                {banner}
              </p>
            ) : null
          }}
        </form.Subscribe>

        <form.Subscribe selector={(state) => [state.canSubmit, state.isSubmitting] as const}>
          {([canSubmit, isSubmitting]) => (
            <Button type="submit" variant="primary" isDisabled={!canSubmit || isSubmitting} className="w-full">
              {isSubmitting ? pendingLabel : submitLabel}
            </Button>
          )}
        </form.Subscribe>
      </form>
      {footer ? <p className="text-sm text-muted">{footer}</p> : null}
    </section>
  )
}
