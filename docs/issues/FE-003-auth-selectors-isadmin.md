# FE-003 — Auth selectors: `useIsAdmin` / `useAuthRoles` + de-duplicate the Admin-role rule

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

Add stable, module-level selector functions over the `me` payload (e.g. `selectIsAdmin`,
`selectRoles`) and derived hooks `useIsAdmin` / `useAuthRoles` built on the `me` factory's `select`.
Replace the duplicated `roles.includes('Admin')` logic in the root nav and the Admin route so the
Admin-role rule is defined exactly once and can't drift between call sites.

Selectors are declared at module scope (not recreated each render). This encodes today's single
`Admin` role rule only — no role hierarchy (out of scope).

## Acceptance criteria

- [ ] Module-level selector functions exist for `isAdmin` and roles; `useIsAdmin`/`useAuthRoles` are
      built on the `me` factory.
- [ ] The nav and the Admin route both consume `useIsAdmin` (or the shared selector); no inline
      `roles.includes('Admin')` remains.
- [ ] A unit test covers the selectors against representative `me` payloads (admin, member, null) and
      asserts the derived hooks expose the selected slice.

## Blocked by

- FE-001
