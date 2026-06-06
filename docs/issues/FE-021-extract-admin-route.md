# FE-021 — Extract the Admin route into feature component(s)

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

The Admin route is ~300 lines (user management + AI-provider config). Extract its logic into testable
feature component(s) so the route becomes a thin shell. Composes with FE-006 (the role gate moved to
`beforeLoad`) — the extracted component no longer carries the access check.

## Acceptance criteria

- [ ] Admin user-management and AI-provider-config logic live in feature component(s); the route is a
      thin shell.
- [ ] No behavior change; a dev-browser pass confirms user management and provider config still work.
- [ ] App boots and existing tests stay green.

## Blocked by

- None - can start immediately
