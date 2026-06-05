# 0019 — Refresh-token rotation & durable sessions

**Phase:** 9 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0001, ADR-0005

## What to build

A signed-in User stays signed in indefinitely while active: a short-lived access JWT is silently
renewed by a long-lived, server-side, revocable refresh token. End-to-end this means a User whose
access token has expired can call `POST /api/auth/refresh` and continue without re-signing-in, while
logout, Admin-disable, and password-change remain hard revocation points.

- **`RefreshTokenService`** (deep module): `Issue`, `Rotate`, `RevokeCurrent`, `RevokeAll`. A 256-bit
  random token, **SHA-256-hashed at rest** (raw value never persisted), **rotated on every use**
  (each refresh mints a new token and invalidates the prior), **reuse-detection** (presenting an
  already-rotated token revokes the whole chain), a brief **grace window** for a just-rotated token,
  and a **sliding expiry with no absolute cap**.
- **`RefreshToken`** entity + EF mapping: hashed token, `UserId`, `ExpiresAt`, rotation/replaced-by
  linkage, optional device/user-agent *label*. Not bound to IP.
- **`POST /api/auth/refresh`** rotates the refresh token and mints a new access JWT (web: sets the
  tokens as cookies; mobile: returns the token body).
- **Short access JWT (~15 min)** so revocation takes effect promptly.
- Revocation wiring: **logout** revokes the current device's token; **Admin-disable** and
  **password-change** revoke **all** of a User's refresh tokens.

Cookie prefixes, the `X-CSRF` check, and the client single-flight interceptor are deliberately split
into #0020 so this slice can land and be tested on its own.

## Acceptance criteria

- [ ] `RefreshTokenService` unit tests (domain-style): issue→rotate produces a new token and
      invalidates the prior; presenting a rotated token (reuse) revokes the chain; the grace window
      permits a just-rotated token briefly; the sliding window extends on use and never hard-caps;
      the raw token is never retrievable (stored hashed).
- [ ] After the access token expires, `POST /api/auth/refresh` re-establishes access and rotates the
      refresh token.
- [ ] Logout revokes only the current device's refresh token; the User's other sessions survive.
- [ ] Admin-disable and password-change each revoke **all** of the User's refresh tokens.
- [ ] The access JWT lifetime is ~15 minutes; a disabled User holding a still-valid access token is
      locked out within minutes.
- [ ] Integration tests cover refresh-after-expiry, current-device logout, and all-session revocation
      on disable/password-change.

## Blocked by

- #0002
