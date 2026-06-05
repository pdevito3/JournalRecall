# FORM-012 — Convert Session Metadata editor

**Phase:** 11 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0004

## What to build

Convert the **Session Metadata** editor (Topics, People, Mood) to the new pattern, including the
conditional custom-Mood field — the trickiest form in scope.

- `useForm` + colocated form-level zod schema, composed from `FormShell` + bound field components
  (`TextField` for Topics/People, `SelectField` for Mood, `TextField` for custom Mood).
- **Conditional field:** render the custom-Mood `TextField` only when the selected Mood is `Custom`.
  Enforce "custom value required when Mood is Custom" via `.superRefine` attaching the issue to the
  custom-Mood path — replacing today's imperative button-disable check.
- Schema stays validation-only; the comma-separated **Topics** and **People** inputs are split into
  arrays in the **submit handler**, so metadata is stored correctly without changing how the user
  types. Export `z.infer` for the mutation payload type.
- Validate on blur then live-after-error; submit gated on `canSubmit`/`isSubmitting`.
- A targeted unit test for the custom-Mood `.superRefine` may be added if it proves fiddly.
- **Scope guardrail:** the debounced autosave **Draft** editor (Session Raw/Cleaned textareas) is
  **left as-is** — its per-keystroke save model fights react-form's submit/validation model. Do not
  convert it.

## Acceptance criteria

- [ ] Metadata editor uses `useForm` + colocated validation-only schema composed from `FormShell` +
      field components; the custom-Mood input renders only when Mood is `Custom` and is required in
      that case (enforced via `.superRefine` on the custom-Mood path).
- [ ] Topics and People are split from comma-strings to arrays in the submit handler; the payload type
      derives from `z.infer`.
- [ ] The autosave Draft (Raw/Cleaned) editor is untouched; manual pass confirms conditional field,
      required-when-Custom, and array splitting.

## Blocked by

- FORM-003
- FORM-005
