/**
 * `createForm<Schema>()` — the thin per-form factory (ADR-0008, FE-026). It exists only to restore the
 * schema-key typing the shared layer erases: TanStack's `form.Field`/`FormShell` must accept *any* form,
 * so field `name`s decay to plain strings and `applyServerErrors` keys to `string`. This factory closes
 * over a `z.infer<Schema>` value type and hands back:
 *
 * - a `Field` component whose `name` is the schema key union and whose render-prop exposes the field
 *   narrowed to *that key's* value type (a wrong name or a value-type mismatch is a `tsc` error), and
 * - `applyServerErrors` typed against the same key set (the runtime "known field?" check is now a typed
 *   mapping, not `Any*` erasure).
 *
 * Per ADR-0008 we own this ~50-line factory rather than adopting `createFormHook`: it composes with the
 * pieces we already have (the react-aria bound inputs, `<Form>` context, the `applyServerErrors` seam)
 * instead of re-bridging them. It is deliberately a typed *identifier* layer, not a schema-driven
 * renderer — explicit field composition stays; only the types get sharper.
 */

import type { ReactNode } from 'react'
import type { z } from 'zod'
import { useFormContext } from './form-context'
import { applyServerErrors, type TypedFormApi } from './apply-server-errors'

/**
 * The slice of react-form's `FieldApi` the bound inputs actually consume, narrowed to one schema key's
 * value type. Keeping this to the consumed surface (rather than reconstructing react-form's fully
 * parameterized `FieldApi`) is what lets `state.value`/`handleChange` carry `Values[Name]` precisely.
 */
export interface TypedFieldApi<Values, Name extends keyof Values> {
  name: Name
  state: {
    value: Values[Name]
    meta: { errors: readonly unknown[] }
  }
  handleChange: (value: Values[Name]) => void
  handleBlur: () => void
}

interface FieldProps<Values, Name extends keyof Values> {
  /** A schema key — a typo or renamed field is a compile error. */
  name: Name
  /** Render-prop receiving the field narrowed to this key's value type. */
  children: (field: TypedFieldApi<Values, Name>) => ReactNode
}

/** The bundle `createForm<Schema>()` returns: schema-keyed field + server-error mapping. */
export interface FormBundle<Values> {
  /**
   * Schema-keyed field. Reads the form from `<Form>` context and delegates to react-form's `form.Field`;
   * the value the render-prop receives is `Values[Name]`, so passing it to a wrong-typed bound input
   * fails at compile time.
   */
  Field: <Name extends keyof Values>(props: FieldProps<Values, Name>) => ReactNode
  /** `applyServerErrors` typed to this schema's key union — same runtime behavior as the shared helper. */
  applyServerErrors: (form: TypedFormApi<Values & Record<string, unknown>>, error: unknown) => void
}

export function createForm<Schema extends z.ZodType>(): FormBundle<z.infer<Schema>> {
  type Values = z.infer<Schema>

  function Field<Name extends keyof Values>({ name, children }: FieldProps<Values, Name>): ReactNode {
    const form = useFormContext()
    // react-form's `Field` types `name` to the (erased) form's keys; the narrow lives on our props and
    // on `TypedFieldApi`, which is the schema-keyed view we hand the consumer.
    const FormField = form.Field as unknown as (props: {
      name: Name
      children: (field: TypedFieldApi<Values, Name>) => ReactNode
    }) => ReactNode
    return <FormField name={name}>{(field) => children(field)}</FormField>
  }

  return {
    Field,
    applyServerErrors: (form, error) => applyServerErrors<Values & Record<string, unknown>>(form, error),
  }
}
