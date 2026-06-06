/**
 * The chrome every form repeats (PRD-0004 / ADR-0007 / ADR-0008): an optional title, the fields
 * (children), a top-level error banner fed by the form's form-level errors (where `applyServerErrors`
 * routes non-field errors), an optional footer, and a submit button gated on `canSubmit`/`isSubmitting`.
 * Presentational only — each form owns its own `useForm`, schema, and submit.
 *
 * `<Form>` is the compound parent: it provides the form instance via context (ADR-0008), so the
 * `Form.Submit` and `Form.Errors` sub-components read it from `useFormContext()` instead of every call
 * site prop-drilling it. `FormShell` is retained as a thin delegate so existing callers keep working
 * until they migrate to the compound API (FE-027).
 */

import type { FormEvent, ReactNode } from 'react'
import { Button } from '@/shared/ui/button'
import { type AnyReactForm, FormProvider, useFormContext } from './form-context'

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

interface FormProps {
  form: AnyReactForm
  title?: ReactNode
  footer?: ReactNode
  /** The fields, typically `form.Field` render-props composing the bound field components. */
  children: ReactNode
  className?: string
}

/** The compound parent: chrome + form-instance context provider. */
export function Form({ form, title, footer, children, className }: FormProps) {
  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    event.stopPropagation()
    void form.handleSubmit()
  }

  return (
    <FormProvider value={form}>
      <section className={className ?? 'mx-auto max-w-sm space-y-6'}>
        {title ? <h1 className="text-2xl font-semibold text-content">{title}</h1> : null}
        <form onSubmit={handleSubmit} className="space-y-4" noValidate>
          {children}
        </form>
        {footer ? <p className="text-sm text-muted">{footer}</p> : null}
      </section>
    </FormProvider>
  )
}

/** The form-level error banner, fed by the form's form-level errors (where `applyServerErrors` routes non-field errors). */
function FormErrors() {
  const form = useFormContext()
  return (
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
  )
}

interface FormSubmitProps {
  children: string
  /** Rendered on the submit button while the form is submitting. */
  pendingLabel?: string
}

/** The submit button, gated on `canSubmit`/`isSubmitting`. */
function FormSubmit({ children, pendingLabel = 'Working…' }: FormSubmitProps) {
  const form = useFormContext()
  return (
    <form.Subscribe selector={(state) => [state.canSubmit, state.isSubmitting] as const}>
      {([canSubmit, isSubmitting]) => (
        <Button type="submit" variant="primary" isDisabled={!canSubmit || isSubmitting} className="w-full">
          {isSubmitting ? pendingLabel : children}
        </Button>
      )}
    </form.Subscribe>
  )
}

Form.Errors = FormErrors
Form.Submit = FormSubmit

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

/**
 * Retained delegate over the compound `<Form>` so existing prop-based callers keep working until they
 * migrate to `<Form>` + `Form.Errors`/`Form.Submit` (FE-027).
 */
export function FormShell({ form, submitLabel, title, pendingLabel, footer, children, className }: FormShellProps) {
  return (
    <Form form={form} title={title} footer={footer} className={className}>
      {children}
      <Form.Errors />
      <Form.Submit pendingLabel={pendingLabel}>{submitLabel}</Form.Submit>
    </Form>
  )
}
