# FORM-008 — Convert change-password form

**Phase:** 11 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0004

## What to build

Convert the **change-password** form (current + new + confirm) to the new pattern, including the
forced-change-on-first-login path, so password changes are consistent and safe.

- `useForm` + colocated form-level zod schema importing the shared password fragment (policy + match),
  composed from `FormShell` + bound field components.
- Validate on blur then live-after-error; submit gated on `canSubmit`/`isSubmitting`.
- Cover both entry points: voluntary change and the forced-change-on-first-login flow (per
  ADR-0024) — same form, same validation.
- Server errors via `applyServerErrors` (e.g. wrong current password → its field or the banner per
  what the server names).
- Keep the existing mutation and any post-change orchestration.

## Acceptance criteria

- [ ] Change-password uses `useForm` + colocated schema with the shared password fragment, composed
      from `FormShell` + field components; the duplicated match check is gone.
- [ ] Both the voluntary and forced-change-on-first-login paths use this form and enforce policy +
      match; submit is gated until valid.
- [ ] Server errors surface via `applyServerErrors`; manual pass confirms gating, inline errors, and
      both entry points.

## Blocked by

- FORM-003
- FORM-004
- FORM-005
