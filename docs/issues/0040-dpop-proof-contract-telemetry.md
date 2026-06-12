# 0040 — DPoP proof contract doc, error contract & telemetry

**Type:** AFK · **Status:** done · **Realizes:** [PRD-0002](../prd/0002-dpop-sender-constrained-bearer-tokens.md)

## Parent

[PRD-0002 — DPoP / sender-constrained bearer tokens](../prd/0002-dpop-sender-constrained-bearer-tokens.md)

## What to build

Make the bearer DPoP contract implementable by the future iOS app (separate repo — no Swift here)
and observable by the operator.

- **Reference client flow doc** (lives with the mobile docs alongside the offline-first sync
  contract): key pair generated once in Secure Enclave / Android Keystore (non-exportable, ES256
  default); per-request proof shape (header `{ typ: "dpop+jwt", alg, jwk }`, claims
  `{ htm, htu, iat, jti }`, sent in the `DPoP` header, fresh per request); login → bound token +
  bound chain; refresh from the same key; key rotation/reinstall = sign in again with the new key
  to establish a new bound session.
- **Error vocabulary**: document every rejection a client must distinguish — stale/future proof
  (regenerate and retry; note the server-nonce mode hook if ever enabled), replayed proof, key
  mismatch, chain revoked (fall back to fresh sign-in) — with the actual status codes and
  `WWW-Authenticate` challenges the server emits.
- **Telemetry**: DPoP rejections (proof failures, replays, key mismatches) surface in the existing
  auth telemetry without exposing token or proof contents.
- **Guarantee sweep**: verify the standing auth invariants hold for a DPoP-bound session — the
  Privacy invariant, Admin-disable and password-change revoke the bound chain, logout-this-device
  revokes only the bound chain it targets.

## Acceptance criteria

- [x] The reference proof-flow doc exists and covers key lifecycle, proof shape, login, refresh,
      and every documented error with its recovery action.
- [x] Documented error responses match observed server behavior (asserted by integration tests,
      including the `WWW-Authenticate` challenge contract).
- [x] DPoP rejections appear in existing auth telemetry; no token or proof material is logged.
- [x] **Integration:** Admin-disable and password-change revoke a DPoP-bound session; logout
      revokes only the current bound chain; tenant isolation holds for requests authenticated via
      bound tokens.

## Blocked by

- #0037, #0038, #0039
