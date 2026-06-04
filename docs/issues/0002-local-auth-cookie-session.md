# 0002 — Local auth: register/login → cookie session

**Phase:** 1 · **Type:** AFK · **Status:** todo · **Realizes:** ADR-0002

## What to build

A user can register and log in with email + password and receive an authenticated session, with the
first-party JWT delivered as a strict HttpOnly cookie. Replaces the default Identity Razor UI with
React routes under `/app`.

- ASP.NET Core Identity (`User`) on SQLite, password hashing.
- On successful login, mint a first-party **JWT** and set it as a **strict, HttpOnly, Secure,
  SameSite cookie**. `JwtBearer` validates requests via `OnMessageReceived`, reading the token from
  the cookie **or** the `Authorization` header (so mobile bearer clients work later).
- `GET /api/me` returns the current user when authenticated, 401 otherwise.
- React **login** and **register** routes under `/app`; logout clears the cookie.

## Acceptance criteria

- [ ] Registering then logging in sets an HttpOnly auth cookie (not readable from `document.cookie`).
- [ ] `GET /api/me` returns the user with the cookie present and 401 without it.
- [ ] The same `GET /api/me` also succeeds when the JWT is supplied as an `Authorization: Bearer`
      header (no cookie) — proving the dual-delivery validation.
- [ ] Logout clears the cookie and subsequent `/api/me` is 401.
- [ ] No Identity Razor pages are reachable; auth happens through the React routes.
- [ ] Integration tests cover register, login, authorized `/api/me`, and unauthorized 401.

## Blocked by

- #0001
