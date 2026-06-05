# FORM-011 — Convert AI-provider config form

**Phase:** 11 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0004

## What to build

Convert the **AI provider** config form on the Admin surface (provider kind + endpoint + model + API
key) to the new pattern so configuring Cleanup's model is reliable.

- `useForm` + colocated form-level zod schema validating the required fields, composed from
  `FormShell` + bound field components (`SelectField` for provider kind, `TextField` for endpoint /
  model / API key).
- **Seed initial values via react-form `defaultValues`** from the current settings — this replaces
  the existing `useEffect`-to-hydrate-state pattern with a declarative default.
- Validate on blur then live-after-error; submit gated on `canSubmit`/`isSubmitting`.
- Server errors via `applyServerErrors`.
- Keep the existing mutation.

## Acceptance criteria

- [ ] The AI-provider form hydrates from current settings via `defaultValues` (no `useEffect` state
      hydration remains) and validates required fields.
- [ ] Built from `useForm` + colocated schema + `FormShell` + field components; submit gated until
      valid; `applyServerErrors` wired.
- [ ] Manual pass confirms the form loads with current settings, blocks invalid submits, and saves.

## Blocked by

- FORM-003
- FORM-005
