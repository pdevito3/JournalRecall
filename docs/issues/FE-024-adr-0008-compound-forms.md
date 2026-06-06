# FE-024 — ADR-0008: compound form components (supersedes ADR-0007)

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005 · **Introduces:** ADR-0008

## What to build

Write **ADR-0008**, recording the decision to introduce type-safe compound form components and to
**supersede [ADR-0007](../adr/0007-forms-on-tanstack-react-form-zod.md)**. ADR-0007's explicit-`field`-prop
decision was sound at the time, but its cost — the shared layer's `Any*` type erasure (`AnyFieldApi` /
`AnyFormApi`, `as string` casts) and form prop-drilling — is now the thing the compound context +
`createForm<Schema>()` factory removes.

**Decision the ADR must make and justify:** build our own thin `createForm` factory **vs.** adopt
TanStack's `createFormHook`; and how far to push generics for 2–4-field forms. Mark ADR-0007 as
superseded-by ADR-0008.

## Acceptance criteria

- [ ] ADR-0008 exists, states context/decision/consequences, and explicitly supersedes ADR-0007 (which
      is marked superseded-by-0008).
- [ ] The own-factory vs. `createFormHook` choice is made and justified, including the generics-depth
      stance for small forms.
- [ ] Invariants to preserve are listed for the implementation issues (per-form `useForm` + colocated
      zod, existing mutations, Mood `Custom` conditional + comma-split, `SelectField` options-as-prop).

## Blocked by

- None - can start immediately
