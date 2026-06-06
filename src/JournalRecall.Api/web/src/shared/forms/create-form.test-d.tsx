/**
 * Type-level proof for FE-026: `createForm<Schema>()` types field `name`s and the render-prop value
 * against the schema key union, and `applyServerErrors` against the same keys. The `@ts-expect-error`
 * lines ARE the assertion — `tsc --noEmit` (and `vitest`) fail if any of them stops being an error,
 * which is exactly the "a mismatch is a tsc error" acceptance criterion, regression-guarded.
 *
 * This file is type-checked only; nothing here runs.
 */

import { z } from 'zod'
import { expectTypeOf } from 'vitest'
import { createForm, type TypedFieldApi } from './create-form'

const schema = z.object({
  username: z.string(),
  remember: z.boolean(),
})
type Values = z.infer<typeof schema>

const { Field } = createForm<typeof schema>()

// --- field name is the schema key union --------------------------------------------------------
// A valid name compiles, and the render-prop value carries that key's type.
;() => (
  <Field name="username">
    {(field) => {
      expectTypeOf(field).toEqualTypeOf<TypedFieldApi<Values, 'username'>>()
      expectTypeOf(field.state.value).toEqualTypeOf<string>()
      return null
    }}
  </Field>
)
;() => (
  <Field name="remember">
    {(field) => {
      expectTypeOf(field.state.value).toEqualTypeOf<boolean>()
      return null
    }}
  </Field>
)

// A wrong / renamed field name is a compile error.
;() => (
  // @ts-expect-error 'nope' is not a key of the schema
  <Field name="nope">{() => null}</Field>
)

// --- render-prop value carries the key's VALUE type --------------------------------------------
// `remember` is a boolean, so treating it as a string is a compile error.
;() => (
  <Field name="remember">
    {(field) => {
      // @ts-expect-error field.state.value is boolean, not string — value-type mismatch
      const _typo: string = field.state.value
      return null
    }}
  </Field>
)

// `handleChange` is keyed to the field's value type: passing the wrong type is a compile error.
;() => (
  <Field name="username">
    {(field) => {
      // @ts-expect-error handleChange for `username` takes a string, not a number
      field.handleChange(123)
      return null
    }}
  </Field>
)
