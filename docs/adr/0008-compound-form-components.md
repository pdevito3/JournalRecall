# Type-safe compound form components

## Status

accepted (supersedes [ADR-0007](0007-forms-on-tanstack-react-form-zod.md))

## Context & decision

Realizes [PRD-0005](../prd/0005-frontend-pattern-hardening.md). [ADR-0007](0007-forms-on-tanstack-react-form-zod.md)
adopted `@tanstack/react-form` v1 + `zod` v4 with **bound field components** (`TextField`,
`SelectField`, `CheckboxField`), a shared `FormShell`, and an explicit `field` prop threaded from a
`form.Field` render-prop. That decision was sound for landing the pattern, but two costs have shown up
now that all eight forms are converted:

- **`Any*` type erasure in the shared layer.** Because the bound components and `FormShell` must accept
  *any* form/field, the shared code is typed with `AnyFieldApi` / `AnyFormApi` and leans on `as string`
  casts at the react-aria↔react-form bridge. Field names are plain strings, so a typo or a renamed
  schema key is not a compile error.
- **Form prop-drilling.** `form` (and often the submit/error state) is passed down through `FormShell`
  and each `form.Field`, which is noise at every call site and couples the chrome to the form instance
  by hand.

We adopt **type-safe compound form components** built on a small factory, keeping everything else from
ADR-0007.

- **Build our own thin `createForm<Schema>()` factory — NOT TanStack's `createFormHook`.**
  `createForm<Schema extends ZodType>()` returns a per-form bundle: a `useForm`-backed `<Form>`
  provider, schema-typed field components, and `Form.Submit` / `Form.Errors`. The form instance flows
  through **React context**, not props; a **throwing `useFormContext()`** is the single accessor (it
  throws with a clear message when used outside `<Form>`, so misuse is a loud runtime error, not an
  `undefined`). Field `name`s are typed against `z.infer<Schema>` keys, so a wrong/renamed field is a
  compile error, and `applyServerErrors` is typed to the same key set.

  We rejected TanStack's `createFormHook`/`fieldContext` for three reasons: (1) it still requires us to
  pre-register each wrapped field component and a form-context shape — comparable boilerplate to our
  factory but on *their* conventions; (2) our factory composes with the pieces we already own (the
  react-aria-bound fields, `FormShell` chrome, and the `ProblemError`/`applyServerErrors` seam) rather
  than re-bridging them; (3) a ~50-line factory we control is more debuggable and easier to evolve than
  conforming to a third-party hook-factory's generics. The cost we accept is maintaining a small amount
  of generic plumbing ourselves.

- **Generics depth — typed identifiers, not a typed renderer.** Push generics exactly far enough to
  type field `name`s and values off `z.infer<Schema>` and to type `applyServerErrors(form, error)`
  against those keys. Do **not** push toward a schema-driven auto-renderer or per-field value-type
  gymnastics — for 2–4-field forms that machinery costs more than it returns and fights conditional
  fields (the Custom-Mood case). Explicit field composition stays; only the *types* and the
  *form-instance plumbing* get sharper. The `Any*` types and `as string` casts are removed from the
  shared layer.

## Invariants to preserve (for the implementation issues FE-025 → FE-027)

The migration is mechanical and must not change form behavior. Preserve:

- **Per-form `useForm` + colocated, validation-only zod schema** (one `z.object` per form, exported
  `z.infer` type). No transforming schemas.
- **The existing mutations** behind each form (login, register, first-run setup, change-password,
  Corrections create, admin create-user, AI-provider config, Session Metadata editor) — unchanged.
- **The Mood `Custom` conditional + comma-split** in the Metadata editor (custom value required when
  Mood is Custom; Topics/People split to arrays in the submit handler).
- **`SelectField` options-as-prop** (and the other bound fields' react-aria bridge, a11y, and error
  rendering).
- **The `ProblemError` + `applyServerErrors` server-error seam** (field errors mapped onto matching
  fields, everything else to the form-level banner; `.message` preserved as a flattened fallback).
- **The entity-identity keying** of the Metadata + AI-provider forms (ADR-0005 / FE-014): a refetched
  server value re-seeds the form via `key`, not a hydration effect.
- **`FormShell` chrome** (title, footer, top-level error banner, `canSubmit`/`isSubmitting`-gated
  submit) — now surfaced through `Form.Submit` / `Form.Errors` rather than props.

## Considered options

- **Keep ADR-0007's explicit-`field`-prop level** — no new abstraction, but the `Any*` erasure and
  prop-drilling stay. Rejected: this is the problem being addressed.
- **Adopt `@tanstack/react-form`'s `createFormHook` + `fieldContext`** — removes prop-drilling via
  their context, but on their conventions and still needs per-field wrapper registration; doesn't
  compose as cleanly with our existing fields/`FormShell`/`applyServerErrors`. Rejected for a thinner
  factory we own.
- **Full generic, schema-driven field renderer** — maximal type-safety and DRY, but heavy machinery
  for eight small forms and fights conditional fields. Rejected as over-engineered (same reasoning that
  rejected the auto-renderer in ADR-0007).

## Consequences

- The shared form layer loses its `AnyFieldApi`/`AnyFormApi`/`as string` erasure; field names and
  server-error keys are checked against the schema at compile time. Renaming a schema field surfaces as
  a type error at every call site.
- Call sites shrink: `form` no longer threads through `FormShell`/`form.Field`; components read it from
  context, and a misuse outside `<Form>` throws immediately.
- ADR-0007's dependency, schema, fragment, and server-error decisions are retained verbatim — ADR-0008
  changes only the binding/abstraction layer, so the migration (FE-027) is a refactor with no
  user-visible change.
- We take on ~50 lines of generic factory plumbing as code we maintain, in exchange for dropping the
  `Any*` types and the per-call-site prop-drilling.
