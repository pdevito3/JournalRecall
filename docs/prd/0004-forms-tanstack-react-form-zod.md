# PRD 0004 — Forms on @tanstack/react-form + zod

**Status:** ready-for-agent · **Type:** AFK · **Delivery:** big-bang (shared modules first, then
convert all forms) · **Realizes:** a proposed forms-pattern ADR

> Domain language per [`CONTEXT.md`](../../CONTEXT.md): the forms in scope edit or create
> **Correction**s (canonical term + mishearings + hard-replace), **Session** **Metadata**
> (**Topic**/**Person**/**Mood** — Mood being a SmartEnum value object with a `Custom` free-text
> member), the AI provider/model config on the **Admin** surface, and **User** identity flows
> (login, register, first-run **setup**, change-password, admin create-user). Auth flows per
> [ADR-0002](../adr/0002-cookie-wrapped-jwt-auth.md),
> [ADR-0021](../adr/0021-first-run-setup-root-admin.md),
> [ADR-0023](../adr/0023-operator-controlled-registration.md),
> [ADR-0024](../adr/0024-temp-passwords-forced-change.md). Admin surface per
> [ADR-0016](../adr/0016-admin-surface.md).

## Problem Statement

As a developer on the JournalRecall web client, every form is built from scratch and they all do it
slightly differently:

- **No shared pattern.** Each form hand-rolls 2–4 `useState` fields, a manual `handleSubmit`/`onPress`,
  and ad-hoc validation (`.trim()` checks, `password !== confirm`, `customMood.trim().length === 0`).
  The same password-match check is reimplemented in three places (register, setup, change-password).
- **No declarative validation.** There is no validation library and no schema anywhere in the client.
  Rules live as scattered imperative `if` statements, so they can't be reused, typed, or audited.
- **Inconsistent UX.** Some forms show errors, some only disable the submit button, some gate on
  `isPending`. There is no uniform "touched / dirty / invalid / submitting" behavior.
- **Server errors are destroyed before the UI sees them.** The API client's `problem()` helper eagerly
  flattens an ASP.NET `ValidationProblemDetails` response into a single joined `Error.message`, so the
  per-field `errors` dict is gone by the time a form's `onError` runs. Field-level server errors are
  impossible to surface.
- **No single place to fix accessibility, error rendering, or wiring.** Inputs are wired to
  react-aria-components by hand per form, so a11y and error-display quality vary.

## Solution

As a developer, I get **one established, reusable pattern** for forms and validation, built on
`@tanstack/react-form` + `zod`, so I stop reinventing the wheel:

- **Bound field components** (`TextField`, `SelectField`, `CheckboxField`) that bridge react-form to
  the existing react-aria-components inputs once, and delegate error display + a11y to react-aria.
- **A shared `FormShell`** for the chrome every form repeats (title, footer, top-level error banner,
  submit button gated on `canSubmit`/`isSubmitting`).
- **One form-level `zod` schema per form**, colocated with the form, validating on blur (then live
  after a field has errored) with a final submit guard. Cross-field and conditional rules
  (password match; "custom Mood value required when Mood is Custom") expressed via `.refine`/
  `.superRefine`.
- **Shared schema fragments** for the rules that are genuinely one decision (password policy +
  match, email), imported by the forms that should change together.
- **A structured server-error path:** the API client throws a `ProblemError` carrying the parsed
  **ProblemDetails** object (`.message` preserved as a flattened fallback so existing readers don't
  break), and a shared `applyServerErrors(form, error)` helper maps the `errors` dict onto matching
  fields and routes everything else (or a bare `title`/`detail`) to the form-level banner.

Delivered **big-bang**: build the shared modules, then convert all eight real forms in one pass.
Forms are similar enough and few enough that proving on a pilot first would cost more than it saves.

## User Stories

### The reusable pattern
1. As a developer, I want a single documented forms pattern, so that I build new forms by composing
   shared pieces instead of hand-rolling state and validation.
2. As a developer, I want bound `TextField`/`SelectField`/`CheckboxField` components, so that wiring
   a react-aria input to react-form is done once and reused, not re-derived per form.
3. As a developer, I want the field components to take the react-form `field` via an explicit prop
   (no `createFormHook`/context magic), so that the data flow is obvious and easy to read and debug.
4. As a developer, I want `TextField` to cover text/password/email via a `type` prop (no separate
   `PasswordField`), so that the component set stays small and the pattern stays legible.
5. As a developer, I want a `FormShell` that renders the title, footer, top-level error banner, and a
   submit button gated on `canSubmit`/`isSubmitting`, so that every form looks and behaves the same.
6. As a developer, I want the field components to delegate error text and `aria-invalid`/
   `aria-describedby` to react-aria-components, so that accessibility is correct and consistent for
   free.
7. As a developer, I want the field component to bridge react-aria's `onChange(value)` (raw value, not
   a DOM event) and `onBlur` to `field.handleChange`/`field.handleBlur`, so that no form repeats that
   bridge.

### Validation
8. As a user, I want a field to validate when I leave it and then update live once it has shown an
   error, so that I get timely feedback without being nagged on the first keystroke.
9. As a user, I want the submit button disabled until the form is valid and not already submitting, so
   that I can't fire an obviously-invalid request.
10. As a developer, I want one form-level `zod` schema per form colocated with that form, so that a
    form's rules live in one declarative place next to where they're used.
11. As a developer, I want password rules (policy + match) and the email rule as shared schema
    fragments, so that changing the password policy is one edit that applies everywhere it should.
12. As a developer, I want schemas to be validation-only and to transform comma-separated inputs
    (Topics, People, Correction mishearings) into arrays at submit time, so that the field value type
    matches what the field components write.
13. As a developer, I want a form's initial values seeded via react-form `defaultValues`, so that the
    AI-provider form's `useEffect`-to-hydrate-state pattern is replaced with a declarative default.

### Server errors
14. As a user, I want a server validation error to appear under the field that caused it when the
    server names that field, so that I know exactly what to fix.
15. As a user, I want a non-field server error (e.g. "invalid credentials") to appear as a single
    banner at the top of the form, so that errors that aren't field-scoped read correctly.
16. As a developer, I want the API client to throw a `ProblemError` carrying the parsed ProblemDetails
    object, so that field-level error data survives the trip from fetch to the form's `onError`.
17. As a developer, I want `ProblemError.message` to keep the flattened fallback string, so that any
    existing code reading `error.message` keeps working.
18. As a developer, I want one `applyServerErrors(form, error)` helper wired identically into every
    form, so that server-error handling is uniform and written once.

### The eight forms
19. As a User, I want the **login** form (email + password) to validate and surface errors with the new
    pattern, so that sign-in behaves consistently with the rest of the app.
20. As a User, I want the **register** form (email + password + confirm, with match validation) to use
    the shared password fragment, so that registration enforces the same password rules as everywhere
    else.
21. As an operator, I want the first-run **setup** form to use the same fields and password fragment as
    register while remaining its own form, so that bootstrapping the root Admin stays independent of
    self-registration's lifecycle.
22. As a User, I want the **change-password** form (current + new + confirm) to validate match and
    policy with the shared fragment, including the forced-change-on-first-login path, so that password
    changes are consistent and safe.
23. As a User, I want the **Corrections** create form (canonical term + mishearings + hard-replace
    checkbox) to validate that a canonical term is present and split mishearings at submit, so that I
    can't create an empty Correction.
24. As an Admin, I want the **create-user** form (email + password + role) to surface per-field server
    errors (e.g. email already taken) under the right field, so that I can correct admin-created
    accounts quickly.
25. As an Admin, I want the **AI provider** config form (provider kind + endpoint + model + API key) to
    hydrate from current settings via `defaultValues` and validate required fields, so that configuring
    Cleanup's model is reliable.
26. As a User, I want the **Session Metadata** editor (Topics, People, Mood) to render the custom-Mood
    text input only when Mood is `Custom` and require a value in that case, so that a Custom Mood always
    carries its free-text value.
27. As a User, I want the comma-separated Topics/People inputs to be split into arrays on save, so that
    metadata is stored correctly without changing how I type it.

### Scope guardrails
28. As a developer, I want single-instant-submit controls (the registration enable/disable toggle,
    per-user role select, admin reset-password field) left as-is, so that I don't wrap zero-validation
    one-shot controls in form machinery.
29. As a developer, I want the debounced autosave **Draft** text editor (Raw/Cleaned) left as-is, so
    that react-form's submit/validation model doesn't fight the live per-keystroke save model.

## Implementation Decisions

**Dependencies.** Add `@tanstack/react-form` (v1.x latest) and `zod` (v4.x latest). No
`@tanstack/zod-form-adapter` — react-form v1 consumes zod schemas directly as Standard Schema
validators. Compatible with the existing React 19.2 / Vite / TypeScript setup.

**Abstraction level.** "Level B" — thin bound field components over react-aria-components, composed by
each form. Not raw react-form per form (doesn't kill the boilerplate, just moves it), and not a
schema-driven auto-renderer (too much machinery for eight forms and fights conditional fields).

**Field binding.** Explicit `field` prop passed into each bound component from a `form.Field`
render-prop. No `createFormHook`/`fieldContext`. Chosen for legibility over minimal call-site code at
this scale.

**Modules to build (shared):**

- **ProblemDetails parser + `ProblemError`** — *deep module.* Interface: takes an HTTP `Response`
  (and a fallback message), returns/throws a `ProblemError` whose `.problem` is the parsed
  ProblemDetails object and whose `.message` is the existing flattened fallback string. Replaces the
  per-module `problem()` flattening in the API client layer (auth + admin). Designed against the
  current ASP.NET `ValidationProblemDetails` shape (`type`/`title`/`detail`/`status` + `errors` dict)
  and forward-compatible with the project's upcoming ProblemDetails standardization.
- **`applyServerErrors(form, error)`** — *deep module.* Interface: given a react-form instance and a
  caught error, if it is a `ProblemError` with an `errors` dict, map each entry's key (server casing →
  field name) onto the matching form field's errors; route unmatched keys, a bare `title`/`detail`, or
  any non-`ProblemError` to a single form-level error banner. Wired into every form's `onError`/
  `onSubmitInvalid`.
- **Shared schema fragments** — *deep module(s).* Pure zod: a password fragment (policy + the match
  `.refine`) and an email fragment, imported by the forms that must change together (register, setup,
  change-password share password; auth forms share email). Coupling test: extract only what should
  change together.

**Modules to build (reusable UI):**

- **Bound field components** — `TextField` (with `type` prop: text/password/email), `SelectField`,
  `CheckboxField`. Each wraps the corresponding react-aria-components input, takes the react-form
  `field` + presentational props (label, etc.), bridges `onChange(value)`/`onBlur` to react-form, and
  maps `field.state.meta.errors` → react-aria `isInvalid` + `errorMessage`/`FieldError`. No
  `TextAreaField`, number, or date field (not needed by the in-scope forms).
- **`FormShell`** — presentational chrome: title, footer slot, top-level error banner, submit button
  bound to `canSubmit`/`isSubmitting`.

**Consolidation (coupling test — consolidate only what should change together):**

- Shared chrome (`FormShell`), shared schema fragments, and shared field components are consolidated.
- Every form keeps its **own** `useForm` + schema + submit. Register and setup look alike but are
  **kept separate** — self-registration and first-run bootstrap have independent lifecycles and will
  likely diverge; they merely import the same password fragment.
- The existing shared `AuthForm` component is superseded by `FormShell` + field components + per-form
  schemas; the three auth flows become independent forms composing those shared pieces.

**Validation timing.** Zod schema attached as the form-level `onBlur` validator; react-form
revalidates on change once a field is touched/errored; submit is guarded and the button is gated on
`canSubmit`/`isSubmitting` (replacing today's ad-hoc per-field disable logic).

**Schema conventions.** One form-level `z.object` per form, colocated with the form/feature.
Validation-only (no transforming schemas) so the inferred input type matches what field components
write; comma-string → array splitting happens in the submit handler. Export `z.infer` types so the
mutation payload type derives from the schema.

**Conditional fields.** The Session Metadata form renders the custom-Mood `TextField` only when the
selected Mood is `Custom`, and enforces "custom value required when Custom" via `.superRefine`
attaching the issue to the custom-Mood path — replacing the imperative button-disable check.

**Server-error seam.** API client owns "HTTP → ProblemDetails object" (throws `ProblemError`); the
form helper owns "ProblemDetails → field/banner errors." The only behavior change for existing
consumers is that errors are now richer; `.message` is unchanged.

**Submission unchanged.** Forms continue to submit via the existing TanStack Query mutations; only the
state/validation/error-surfacing layer changes. Chained flows (register→login, setup→login) keep their
current orchestration.

## Testing Decisions

A good test here verifies **external behavior**, not internals: given inputs/props, assert the
rendered output and the value/error the consumer observes — never assert on react-form internal state
shape or implementation details. Tests should survive a refactor of how the bridge is wired.

Test coverage concentrates on the **shared modules**, where a bug breaks every form at once:

- **ProblemDetails parser + `ProblemError`** — given representative ASP.NET responses
  (`ValidationProblemDetails` with an `errors` dict; a problem with only `title`/`detail`; a
  non-problem/opaque body), assert the parsed `.problem` and the flattened `.message` fallback.
- **`applyServerErrors`** — given a `ProblemError` and a form, assert field-keyed errors land on the
  matching fields, unmatched keys / bare title / non-`ProblemError` land on the form-level banner, and
  server key casing maps to field names.
- **Bound field components** — render `TextField`/`SelectField`/`CheckboxField` and assert: error text
  renders and `isInvalid` is set when the field has errors, `onChange`/`onBlur` propagate the raw
  value, and label/association are present.
- **Schema fragments** — pure-zod assertions on the password fragment (policy + match) and email
  fragment.

Per-form happy-path component tests are **not** part of this scope; instead each converted form gets a
manual pass (submit blocked until valid, inline errors, server-error display). A targeted test may be
added for unusually tricky form logic — specifically the Metadata custom-Mood `superRefine` — if it
proves fiddly.

Prior art: the client uses Vitest 4 + Testing Library React 16 (already installed). Backend
integration/functional tests (see [PRD 0003](0003-three-layer-test-suite-builders-fakers.md)) are
unaffected — they exercise the API, not the form UI — and must stay green.

## Out of Scope

- **Single-instant-submit controls:** the registration enable/disable toggle, per-user role select,
  and admin reset-password field. No real validation; submit on interaction. Left as-is.
- **The autosave Draft editor** (Session Raw/Cleaned textareas): debounced per-keystroke save, no
  submit step. react-form's model fights this; left as-is.
- **Server-side ProblemDetails standardization:** the broader backend work to emit ProblemDetails
  uniformly is tracked separately. This PRD only consumes the (already ProblemDetails-shaped)
  responses on the client. The client `ProblemError` is built now and keeps working as the server
  effort lands.
- **A schema-driven / auto-rendered form system.** Explicitly rejected as over-engineered for this
  surface.
- **Async/uniqueness validation during editing**, file uploads, and any mid-edit network validation —
  none of the in-scope forms need them.

## Further Notes

- Big-bang is chosen deliberately: with the shared modules built first, the eight conversions are
  mechanical, and a pilot-then-fan-out would add overhead without reducing risk at this scale.
- The password-match rule currently duplicated across register, setup, and change-password collapses
  into the single shared password fragment — a concrete win for the "don't reinvent the wheel" goal.
- `ProblemError` is intentionally introduced ahead of the server-side ProblemDetails rollout so the
  forms work gets the structured-error seam now; the two efforts converge rather than block each other.
