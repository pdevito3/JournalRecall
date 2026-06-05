# 0035 — Migrate core auth (login, refresh, cookie hardening, forced change)

**Phase:** 10 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0003

## What to build

Migrate the auth-behavior tests into the functional layer, where they belong: these prove the **real**
auth pipeline and must always use real auth (never the fake-auth bypass). Suite stays green.

- Migrate `AuthTests`, `RefreshTokenTests`, `CookieHardeningTests`, `ForcedPasswordChangeTests` from
  `Api.Tests` into `FunctionalTests/Auth/` (or area subfolders).
- All of these use **real auth** — register→login, refresh rotation, CSRF rejection, `Set-Cookie`
  prefix/attribute assertions, the `403 password_change_required` sentinel block-then-clear. Do **not**
  route them through fake auth.
- Lowercase names; Shouldly assertions; use `ApiRoutes` + the JSON `HttpClient` helpers.

## Acceptance criteria

- [ ] Login, refresh-rotation, cookie-hardening, and forced-password-change tests live in
      `FunctionalTests`, named `lowercase_with_underscores`, using Shouldly and `ApiRoutes`.
- [ ] Every one of these tests uses real auth (no fake-auth scheme).
- [ ] The migrated files are removed from `Api.Tests`; the full suite is green.

## Blocked by

- #0028
