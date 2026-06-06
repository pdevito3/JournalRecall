/**
 * The form instance flows through React context (ADR-0008), not props: `<Form>` provides it and the
 * `Form.*` sub-components read it via the single accessor `useFormContext()`. The context defaults to a
 * missing sentinel so `useFormContext()` can throw a clear, developer-readable error when a `Form.*`
 * sub-component is used outside `<Form>` — misuse is a loud runtime error, not a silent `undefined`.
 *
 * Typing here still uses the `Any*` form type; precise schema typing arrives with FE-026's
 * `createForm<Schema>()` factory, which will narrow this context's value to the per-form instance.
 */

import { createContext, useContext } from 'react'
import type { ReactFormExtendedApi } from '@tanstack/react-form'

/** Any form returned by `useForm` — the shared chrome only reads state and submits. */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export type AnyReactForm = ReactFormExtendedApi<any, any, any, any, any, any, any, any, any, any, any, any>

/** Distinguishes "no provider" from a real form, so the accessor can throw instead of returning `undefined`. */
const MISSING = Symbol('Form context missing')

const FormContext = createContext<AnyReactForm | typeof MISSING>(MISSING)

export const FormProvider = FormContext.Provider

/** The single accessor for the form inside `<Form>`. Throws when used outside `<Form>`. */
export function useFormContext(): AnyReactForm {
  const form = useContext(FormContext)
  if (form === MISSING) {
    throw new Error('Form.* must be used within <Form>')
  }
  return form
}
