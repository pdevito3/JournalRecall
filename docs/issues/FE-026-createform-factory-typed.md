# FE-026 — `createForm<Schema>()` factory + typed `applyServerErrors`

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005 / ADR-0008

## What to build

Add a `createForm<Schema>()` factory that returns a `Field` whose `name` prop is the schema's key union
and whose render-prop field carries that key's value type — restoring the schema-key typing the shared
layer currently erases to `AnyFieldApi` / `as string`. Strengthen `applyServerErrors` against the
schema's static key union so the runtime "is this a known field?" check becomes a typed mapping.

Build per the own-factory-vs-`createFormHook` decision recorded in ADR-0008 (FE-024).

## Acceptance criteria

- [ ] `createForm<Schema>()` returns components whose field `name` is the schema key union and whose
      render-prop value carries the key's type; field/value-type mismatches fail at compile time.
- [ ] `applyServerErrors` is typed against the schema's static key union (no `Any*` erasure on the
      mapping).
- [ ] Existing `applyServerErrors` tests still pass; a field-name/value-type mismatch is a `tsc` error.

## Blocked by

- FE-025
