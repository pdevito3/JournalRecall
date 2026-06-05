# 0036 — Migrate access gate, setup, admin & registration

**Phase:** 10 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0003

## What to build

Migrate the access-gate, first-run-setup, admin-surface, admin-gate, and registration-policy tests.
Most are functional (real auth + the gate); app-global registration/app-settings tests that aren't
User-scoped go to functional or the dedicated serial `GlobalState` collection — not the shared
integration layer. Suite stays green.

- Migrate `AccessGateTests`, `AdminGateTests`, `SetupTests`, `RegistrationControlTests`,
  `AdminSurfaceTests` from `Api.Tests` into `FunctionalTests/{Area}/` (and a `GlobalState` collection for
  app-global registration-policy / app-settings tests).
- Admin-authorization tests may use fake auth (`AsAdmin()`) where they only need "be an Admin to reach
  this endpoint"; gate/setup/registration-policy behavior that *is* the thing under test uses real auth.
- Lowercase names; Shouldly assertions; `ApiRoutes` + JSON helpers.

## Acceptance criteria

- [ ] Access-gate, setup, admin-surface, admin-gate, and registration-control tests live in
      `FunctionalTests` (with app-global cases in a serial `GlobalState` collection), named
      `lowercase_with_underscores`, using Shouldly.
- [ ] Real auth is used where auth/gate/registration behavior is under test; fake auth only for
      "be someone to reach the endpoint" cases.
- [ ] The migrated files are removed from `Api.Tests`; the full suite is green.

## Blocked by

- #0028
