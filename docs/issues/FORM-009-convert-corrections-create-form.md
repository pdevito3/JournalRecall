# FORM-009 — Convert Corrections create form

**Phase:** 11 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0004

## What to build

Convert the **Corrections** create form (canonical term + mishearings + hard-replace checkbox) to the
new pattern so an empty Correction can't be created.

- `useForm` + colocated form-level zod schema, composed from `FormShell` + bound field components
  (`TextField` for canonical term + mishearings, `CheckboxField` for hard-replace).
- Schema validates that a canonical term is present. Schema stays validation-only; the
  comma-separated **mishearings** input is split into an array in the **submit handler** (not via a
  transforming schema), so the field value type matches what the field component writes. Export the
  `z.infer` type so the mutation payload derives from the schema.
- Validate on blur then live-after-error; submit gated on `canSubmit`/`isSubmitting`.
- Server errors via `applyServerErrors`.

## Acceptance criteria

- [ ] Corrections create uses `useForm` + colocated validation-only zod schema composed from
      `FormShell` + field components; a missing canonical term blocks submit with an inline error.
- [ ] Mishearings are split from comma-string to array in the submit handler; the mutation payload
      type derives from the schema's `z.infer`.
- [ ] Submit gating, the hard-replace checkbox, and `applyServerErrors` are wired; manual pass
      confirms an empty Correction cannot be created.

## Blocked by

- FORM-003
- FORM-005
