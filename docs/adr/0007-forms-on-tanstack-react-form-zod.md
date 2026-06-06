# Forms on `@tanstack/react-form` + zod, with bound field components

## Status

accepted

## Context & decision

Realizes [PRD-0004](../prd/0004-forms-tanstack-react-form-zod.md). Every form in the web client
(login, register, first-run setup, change-password, Corrections create, admin create-user, AI-provider
config, Session Metadata editor) was hand-rolled: ad-hoc `useState` fields, a manual
`handleSubmit`/`onPress`, scattered imperative validation, and inconsistent error/UX behavior. The
password-match check was reimplemented in three places, there was no validation library, and the API
client flattened ASP.NET `ValidationProblemDetails` into a single string before any form could surface
per-field errors. We adopt **one established, reusable pattern** built on `@tanstack/react-form`
(v1.x) + `zod` (v4.x).

- **Dependencies.** Add `@tanstack/react-form` and `zod` only. No `@tanstack/zod-form-adapter` —
  react-form v1 consumes zod schemas directly as Standard Schema validators. Compatible with the
  existing React 19 / Vite / TypeScript / react-aria-components setup.
- **Abstraction level — "Level B".** Thin **bound field components** (`TextField` with a `type` prop
  covering text/password/email, `SelectField`, `CheckboxField`) over the existing
  react-aria-components inputs, plus a shared `FormShell` for the chrome (title, footer, top-level
  error banner, submit button). Each form composes these. We rejected raw react-form per form (just
  moves the boilerplate) and a schema-driven auto-renderer (too much machinery for eight forms; fights
  conditional fields).
- **Field binding — explicit `field` prop.** Each bound component takes the react-form `field` via an
  explicit prop from a `form.Field` render-prop. No `createFormHook`/`fieldContext` magic — chosen for
  legibility and debuggability over minimal call-site code at this scale. The component bridges
  react-aria's `onChange(value)` (raw value, not a DOM event) and `onBlur` to
  `field.handleChange`/`field.handleBlur`, and maps `field.state.meta.errors` to react-aria's
  `isInvalid` + `FieldError`, so a11y (`aria-invalid`/`aria-describedby`) and error rendering are
  correct once and reused.
- **Validation timing.** The zod schema is attached as the form-level `onBlur` validator; react-form
  revalidates on change once a field has errored; submit is guarded and the submit button is gated on
  `canSubmit`/`isSubmitting` — replacing today's ad-hoc per-field disable logic.
- **Schema conventions.** One form-level `z.object` per form, colocated with the form/feature, and
  **validation-only** (no transforming schemas) so the inferred input type matches what the field
  components write. Comma-separated inputs (Topics, People, Correction mishearings) are split into
  arrays in the **submit handler**, not via a transforming schema. Export `z.infer` types so the
  mutation payload derives from the schema. Cross-field and conditional rules (password match; "custom
  Mood value required when Mood is Custom") use `.refine`/`.superRefine`.
- **Shared fragments, consolidated only where coupled.** A password fragment (policy + match) and an
  email fragment are extracted as pure zod and imported by the forms that must change together
  (register/setup/change-password share password; auth forms share email). Register and setup look
  alike but stay **separate forms** — self-registration and first-run bootstrap have independent
  lifecycles — they merely import the same fragments.
- **Structured server errors.** The API client throws a `ProblemError` carrying the parsed
  ProblemDetails object (`.message` preserved as a flattened fallback so existing readers keep
  working), and a shared `applyServerErrors(form, error)` helper maps the `errors` dict onto matching
  fields and routes everything else (unmatched keys, a bare `title`/`detail`, a non-`ProblemError`) to
  the form-level banner.

## Considered options

- **Keep hand-rolling each form** — no new dependency, but the duplication, inconsistent UX, and
  destroyed server errors persist. Rejected: this is the problem.
- **Raw `@tanstack/react-form` per form, no bound components** — gains declarative validation but each
  form still re-derives the react-aria↔react-form bridge and the chrome. Rejected: moves boilerplate
  rather than killing it.
- **Schema-driven / auto-rendered form system** — maximal DRY, but heavy machinery for eight forms and
  fights conditional fields (the Custom-Mood case). Rejected as over-engineered for this surface.
- **`@tanstack/zod-form-adapter`** — unnecessary: react-form v1 takes zod schemas directly as Standard
  Schema validators. Rejected as a redundant dependency.

## Consequences

- New forms are built by composing `FormShell` + bound field components + a colocated zod schema, not
  by hand-rolling state and validation. The bridge, a11y, and chrome are written once.
- The duplicated password-match rule collapses into a single shared fragment; changing the password
  policy is one edit that applies everywhere it should.
- Field-level server errors now survive the trip from fetch to the form via `ProblemError` +
  `applyServerErrors`; `.message` is unchanged so existing `error.message` readers keep working. This
  seam is introduced ahead of the broader server-side ProblemDetails standardization and converges
  with it rather than blocking on it.
- The existing shared `AuthForm` component is superseded by `FormShell` + field components + per-form
  schemas; the three auth flows become independent forms composing those shared pieces.
- Out of scope and deliberately left as-is: single-instant-submit controls (registration enable/disable
  toggle, per-user role select, admin reset-password field) and the debounced autosave Draft editor
  (Session Raw/Cleaned textareas), whose per-keystroke save model fights react-form's submit model.
