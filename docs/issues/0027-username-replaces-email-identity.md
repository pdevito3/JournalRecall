# 0027 — Replace email with username as the sole identity

**Phase:** 9 · **Type:** AFK · **Status:** ready

## What to build

Make **username** the sole login identity across the whole app, end-to-end. Email is removed from the
API and UI entirely; users register, log in, are admin-created, and are displayed by username. Kept as
**one atomic slice** because a backend-only or frontend-only half leaves mismatched field contracts and
is not demoable.

End-to-end behavior: on a fresh instance the operator sets up a root Admin with a **username**; users
log in with **username + password**; an Admin creates users by **username**; the username is what shows
in the nav and the admin user list.

- **Construction path** — add a `User.Create(Username)` static factory on the `User` entity as the
  **sole** way to build a User; setup, self-registration, and admin-create all funnel through
  `User.Create(Username.Create(input))`. `UserName` remains a `string` (inherited from Identity); assign
  the value object's `.Value`.
- **Login** — look users up by username (Identity's by-name lookup) instead of by email; the failure
  message becomes "Invalid username or password".
- **Identity config** — set `RequireUniqueEmail = false`. Uniqueness is enforced by Identity's existing
  case-insensitive normalized-username index (no new index). **Leave the inherited `Email` /
  `NormalizedEmail` columns in place, null/unused** — do not drop columns.
- **Backend surface** — the auth credentials, current-session response, admin user DTO, and admin
  create-user request all carry **username** instead of email.
- **Frontend surface** — add a shared `usernameSchema` (charset `[a-zA-Z0-9._-]`, min 3 / max 32)
  alongside the existing fragments; the setup, login, register, and admin create-user forms collect
  **Username** (label "Username", `autoComplete="username"`, not `type="email"`); the current-session
  shape and the nav + admin-list displays use username.
- **Dev data** — wipe the local dev SQLite database so every account has a clean handle (no migration of
  email-as-username rows).
- **Tests** — update the three-layer auth suite (Unit / Integration / Functional), currently keyed on
  email, to username. The auth integration/functional clients must keep using an `https://localhost`
  base address (Secure cookie prefixes won't send over http).

## Acceptance criteria

- [ ] Setup, self-registration, and admin-create all build the User via `User.Create(Username.Create(...))`;
      there is no other production path that sets `UserName` directly from raw input.
- [ ] Login authenticates by username; a bad username/password returns 401 with "Invalid username or
      password".
- [ ] `RequireUniqueEmail = false`; a duplicate username is rejected by Identity; the inherited Email
      columns remain present and null.
- [ ] An invalid username (charset/length) surfaces inline under the username field on setup, register,
      and admin create-user (via the `ValidationException("username", …)` → 422 → `applyServerErrors`
      path).
- [ ] No email field is collected, validated, returned, or displayed anywhere in the API or UI; nav and
      admin user list show username.
- [ ] The three-layer auth suite passes against username; integration/functional auth clients use an
      `https://localhost` base address.

## Blocked by

- #0026
