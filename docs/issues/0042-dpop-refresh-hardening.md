# 0042 — Harden `/api/auth/refresh`: token lookup before proof, bound chains never ride cookies

**Type:** AFK · **Status:** done · **Realizes:** [PRD-0002](../prd/0002-dpop-sender-constrained-bearer-tokens.md) · **Touches:** [ADR-0014](../adr/0014-dpop-sender-constrained-bearer-path.md), [ADR-0005](../adr/0005-refresh-token-rotation-and-cookie-hardening.md), #0039

## Parent

[PRD-0002 — DPoP / sender-constrained bearer tokens](../prd/0002-dpop-sender-constrained-bearer-tokens.md)
(follow-up from code review of #0039)

## What to build

Two refresh-endpoint hardenings, one slice because they reshape the same handler:

- **Unauthenticated work order.** Today the endpoint fully validates a DPoP proof — an ES256
  signature check plus a replay-cache write — before the presented refresh token is even looked up.
  Any anonymous caller with a garbage token and a self-signed proof burns CPU and inserts ~55s-TTL
  entries into the in-memory cache at line rate. Reorder: look the presented token up first (the
  lookup is side-effect-free, so the existing "an invalid proof never burns the rotation" property
  is preserved), and only validate the proof when the token exists.
- **Bound chains never ride cookies.** The body-only invariant for bound sessions is enforced at
  login but not at refresh: a bound refresh token arriving in the refresh cookie with a valid proof
  rotates successfully and writes the **bound** access JWT into the auth cookies — every subsequent
  cookie-fallback request then presents a `cnf`-bound token as plain Bearer, which the resource
  server rejects, wedging the session while each cookie refresh re-mints bound cookies. When the
  rotation reports a bound chain, always return the body `TokenResponse` and never set auth cookies.

## Acceptance criteria

- [x] **Integration:** a refresh with an unknown/garbage token and a *valid* proof is rejected
      **without** consuming the proof's `jti` — a subsequent valid refresh presenting a fresh proof
      with that same `jti` succeeds (proves no replay-cache write happened pre-auth).
- [x] **Integration (regression pin for the existing ordering):** a refresh with a *stale* proof and
      a valid bound refresh token is rejected with the `StaleProof` DPoP challenge, and the same
      refresh token then succeeds with a fresh proof — an invalid proof never burns the rotation.
- [x] **Integration:** a bound refresh token presented via the refresh cookie (with a valid proof)
      never results in `Set-Cookie` of a `cnf`-bound access token — the response is the body
      `TokenResponse` (or the attempt is rejected), and a follow-up cookie-fallback request is not
      wedged.
- [x] **Regression:** unbound (web cookie) refresh behaves exactly as today; existing
      `refresh_token_tests` and all DPoP functional tests pass unmodified.

## Blocked by

None — can start immediately.
