# FORM-005 — Bound field components + `FormShell`

**Phase:** 11 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0004

## What to build

The reusable UI that bridges react-form to the existing react-aria-components inputs once, so no form
re-derives the wiring, the a11y, or the chrome.

- **Bound field components** — `TextField` (with a `type` prop covering text/password/email — no
  separate `PasswordField`), `SelectField`, `CheckboxField`. Each wraps the corresponding
  react-aria-components input, takes the react-form `field` via an **explicit prop** (no
  `createFormHook`/context) plus presentational props (label, etc.), bridges react-aria's
  `onChange(value)` (raw value, not a DOM event) and `onBlur` to `field.handleChange`/
  `field.handleBlur`, and maps `field.state.meta.errors` → react-aria `isInvalid` +
  `errorMessage`/`FieldError`. No `TextAreaField`, number, or date field — not needed by the in-scope
  forms.
- **`FormShell`** — presentational chrome: title, footer slot, top-level error banner, and a submit
  button bound to `canSubmit`/`isSubmitting`.
- Component tests asserting external behavior: error text renders and `isInvalid` is set when the
  field has errors; `onChange`/`onBlur` propagate the raw value; label/association are present. Never
  assert on react-form internal state shape — tests must survive a refactor of the bridge.

## Acceptance criteria

- [ ] `TextField` (text/password/email via `type`), `SelectField`, and `CheckboxField` each take the
      react-form `field` by explicit prop, bridge raw `onChange`/`onBlur`, and delegate error text +
      `aria-invalid`/`aria-describedby` to react-aria-components.
- [ ] `FormShell` renders title, footer, a top-level error banner, and a submit button gated on
      `canSubmit`/`isSubmitting`.
- [ ] Component tests cover error rendering + `isInvalid`, raw-value propagation, and label
      association, asserting observable output rather than internal state.

## Blocked by

- FORM-001
