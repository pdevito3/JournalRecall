# FORM-006 — Convert login form

**Phase:** 11 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0004

## What to build

Convert the **login** form (email + password) to the new pattern so sign-in behaves consistently with
the rest of the app.

- Replace the hand-rolled `useState`/`handleSubmit`/ad-hoc validation with `useForm` + a colocated
  form-level `z.object` schema (importing the shared email fragment), composed from `FormShell` +
  bound field components.
- Validate on blur (then live once a field has errored); gate the submit button on
  `canSubmit`/`isSubmitting`.
- Surface server errors via `applyServerErrors` in `onError`: invalid-credentials renders as the
  top-level banner.
- Keep the existing TanStack Query mutation and any chained orchestration; only the
  state/validation/error-surfacing layer changes.
- The existing shared `AuthForm` is superseded by `FormShell` + field components for this flow.
- Manual pass: submit blocked until valid, inline errors, server-error banner displays.

## Acceptance criteria

- [ ] Login uses `useForm` + a colocated zod schema (with the shared email fragment) composed from
      `FormShell` and bound field components; no hand-rolled `useState`/validation remains.
- [ ] Submit is disabled until valid and not submitting; invalid credentials show as the top-level
      banner via `applyServerErrors`.
- [ ] Submission still goes through the existing mutation/orchestration; manual pass confirms gating,
      inline errors, and server-error display.

## Blocked by

- FORM-003
- FORM-004
- FORM-005
