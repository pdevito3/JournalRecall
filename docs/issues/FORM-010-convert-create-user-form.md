# FORM-010 — Convert create-user (admin) form

**Phase:** 11 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0004

## What to build

Convert the **create-user** form on the Admin surface (email + password + role) to the new pattern,
with emphasis on per-field server errors so an Admin can correct admin-created accounts quickly.

- `useForm` + colocated form-level zod schema (importing the shared email + password fragments where
  applicable), composed from `FormShell` + bound field components (`TextField` for email/password,
  `SelectField` for role).
- Validate on blur then live-after-error; submit gated on `canSubmit`/`isSubmitting`.
- Server errors via `applyServerErrors`: a per-field error such as **email already taken** must land
  **under the email field**, not in the banner.
- Keep the existing mutation.
- **Scope guardrail:** the standalone per-user **role select** and the admin **reset-password** field
  elsewhere on the Admin surface are single-instant-submit controls and are **left as-is** — do not
  wrap them in form machinery.

## Acceptance criteria

- [ ] Create-user uses `useForm` + colocated schema composed from `FormShell` + field components
      (incl. `SelectField` for role); submit is gated until valid.
- [ ] A server "email already taken" (and other named-field) error renders under the matching field
      via `applyServerErrors`.
- [ ] The per-user role select and admin reset-password field are untouched; manual pass confirms
      gating, inline + per-field server errors.

## Blocked by

- FORM-003
- FORM-004
- FORM-005
