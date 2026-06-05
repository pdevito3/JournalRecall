# FORM-007 — Convert register + setup forms

**Phase:** 11 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0004

## What to build

Convert the **register** form (email + password + confirm, with match validation) and the first-run
**setup** form to the new pattern. They look alike but stay **two separate forms** — self-registration
and first-run root-Admin bootstrap have independent lifecycles and will likely diverge. They merely
import the **same shared password fragment** (policy + match) and email fragment, so the duplicated
password-match check collapses into one place.

- Each form: its own `useForm` + colocated form-level zod schema (importing the shared password +
  email fragments) + own submit, composed from `FormShell` + bound field components.
- Validate on blur then live-after-error; submit gated on `canSubmit`/`isSubmitting`.
- Server errors via `applyServerErrors` (field-level under the matching field; everything else to the
  banner).
- Keep existing mutations and chained flows (register→login, setup→login) and their orchestration.

## Acceptance criteria

- [ ] Register and setup are each their own `useForm` + colocated schema + submit, composed from
      `FormShell` + field components; they import the shared password and email fragments rather than
      re-implementing the match check.
- [ ] The two remain separate forms (not consolidated); password match is enforced via the shared
      fragment in both.
- [ ] Submit gating, inline errors, and `applyServerErrors` wiring are in place; chained
      register→login / setup→login orchestration still works; manual pass confirms each.

## Blocked by

- FORM-003
- FORM-004
- FORM-005
