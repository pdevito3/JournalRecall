# 0024 — Temporary passwords & forced password change

**Phase:** 9 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0001

## What to build

An Admin can onboard or recover a User with a temporary password the User is forced to replace on
first sign-in, with no email stack. End-to-end: an Admin creates/resets a User with a temp password
and a role; that User signs in, is confined to a "set new password" screen until they change it, and
once changed drops into the app — exactly once.

- **`User.MustChangePassword`** flag. Admin-created Users and Admin resets set a temp password + the
  flag.
- **Admin create/reset** flow: type a temporary password to share out-of-band, and assign the
  **Admin** or **Member** role on creation.
- **Change-own-password endpoint** (net-new): clears `MustChangePassword`, and revokes the User's
  **other** sessions (reuses #0019's `RevokeAll`-style behavior). Reused by both the forced-change
  flow and Admin-driven reset.
- **Sentinel enforcement**: on login the User gets a normal session, but the server rejects all calls
  with **`403 password_change_required`** except a small allowlist (change-own-password, refresh,
  logout, `/me`, `/api/auth/config`). The SPA confines the User to a "set new password" screen.

## Acceptance criteria

- [ ] An Admin can create a User with a temporary password and an assigned role (Admin or Member),
      and can reset an existing User to a new temporary password.
- [ ] A User with `MustChangePassword` set is blocked from all non-allowlisted calls/pages with
      `403 password_change_required` until they change their password.
- [ ] Setting the new password clears the flag, drops the User into the app, and revokes the User's
      other sessions.
- [ ] An Admin reset puts the User back into the forced-change state.
- [ ] Integration tests cover the sentinel block-then-clear, the Admin create-with-temp-password and
      reset flows, and role assignment on creation.

## Blocked by

- #0019
- #0016
