# FE-006 — Move the Admin role gate into the Admin route `beforeLoad`

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

Move the Admin role gate from an in-component "no access" message to navigation time. The Admin
route's `beforeLoad` calls `ensureQueryData` on the `me` factory and `throw redirect` to the journal
for non-admins; the in-component access check is deleted. A non-admin **Member** is redirected before
the Admin page renders (no flash of the wrong page); an Admin is admitted.

Use the shared `isAdmin` selector/rule from FE-003 so the gate and the nav share one definition.

## Acceptance criteria

- [ ] Admin route `beforeLoad` ensures the `me` factory and redirects non-admins to the journal; the
      in-component access-denied check is removed.
- [ ] A functional test (three-layer harness, user bound at the DbContext, https base address) asserts
      a non-Admin Member is redirected away and an Admin is admitted.
- [ ] The gate uses the shared Admin-role rule from FE-003, not a fresh `roles.includes('Admin')`.

## Blocked by

- FE-001
- FE-003
