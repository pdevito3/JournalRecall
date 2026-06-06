# FE-027 — Migrate existing forms to the compound API

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005 / ADR-0008

## What to build

Migrate the existing forms (login, register, setup, change-password, corrections-create, create-user,
AI-provider config, Session Metadata) off the prop-drilled `field`/`AnyFieldApi` wiring onto the
compound `<Form>` + `createForm<Schema>()` API. Superseding ADR-0007 changes the *wiring*, not the
*validation behavior*.

**Invariants preserved:** per-form `useForm` + colocated zod schema + existing TanStack Query mutations
unchanged; **Mood**'s `Custom`-member conditional field and the comma-string → array split
(**Topic**/**Person**/**Correction** mishearings) keep working; `SelectField` options stay a prop (not
a `<Select.Option>` compound); register and **setup** remain separate forms sharing fragments.

## Acceptance criteria

- [ ] All existing forms compose from `<Form>` + `createForm` components; no `AnyFieldApi`/`AnyFormApi`
      / `as string` casts remain in the shared form layer or call sites.
- [ ] Mood `Custom` conditional field, comma-string→array split, and `SelectField` options-as-prop
      behave exactly as before; existing form/field/schema tests stay green.
- [ ] A dev-browser pass confirms each migrated form submits and surfaces field + banner errors.

## Blocked by

- FE-026
