# DPoP proof contract for bearer clients (mobile reference flow)

**Realizes:** [PRD-0002](../prd/0002-dpop-sender-constrained-bearer-tokens.md) ·
**Decision:** [ADR-0014](../adr/0014-dpop-sender-constrained-bearer-path.md) ·
**Verified by:** `tests/JournalRecall.FunctionalTests/Auth/dpop_*.cs` — every status code and
`WWW-Authenticate` value below is asserted against the real server, so a drift here is a failing test.

The contract the iOS app (separate repo — no Swift here) implements to hold a **sender-constrained
bearer session**: every token it receives is bound to a device key, and every request must prove
possession of that key. The web SPA ignores all of this — the cookie flow is deliberately unbound.

## Key lifecycle

- Generate **one ES256 (P-256) key pair** in device-backed secure storage — Secure Enclave (iOS) /
  Android Keystore — marked **non-exportable**. The private key never leaves the device.
- The key identifies the *session binding*, not the user: losing it (reinstall, keystore reset,
  deliberate rotation) is recovered by **signing in again with a new key**, which establishes a new
  bound session. There is no key-migration endpoint.
- The server knows the key only as its **RFC 7638 JWK thumbprint** (`jkt`, base64url SHA-256).

## The proof JWT

A fresh proof is minted **per request** (proofs are short-lived and endpoint-specific) and sent in
the **`DPoP` request header** (exactly one — multiple `DPoP` headers are rejected):

```
header: { "typ": "dpop+jwt", "alg": "ES256", "jwk": { <public key: kty, crv, x, y> } }
claims: {
  "htm": "<HTTP method, exact case — e.g. POST>",
  "htu": "<absolute URL: scheme + host + path; no query/fragment>",
  "iat": <unix seconds, now>,
  "jti": "<unique id per proof, e.g. UUID>",
  "ath": "<base64url(SHA-256(access token))>"   // ONLY when presenting an access token
}
```

- **Freshness:** the server accepts `iat` within a **5 s proof lifetime ± 25 s clock skew**, and each
  `jti` is accepted **once** within that window (replay cache).
- **`ath`** is required on every protected-resource request (the proof is bound to the exact token it
  accompanies). Login and refresh proofs carry no `ath` — there is no access token yet.
- The `jwk` header must hold the **public** key only; a private-key member (`d`) is rejected.

## Flow

1. **Login** — `POST /api/auth/login` with the credentials JSON plus a `DPoP` proof header
   (`htm: POST`, `htu: …/api/auth/login`). On success the response is a JSON body (no cookies):
   `{ accessToken, refreshToken, refreshTokenExpiresAt }`. The access token carries
   `cnf: { jkt: "<your key's thumbprint>" }` and the refresh chain is bound to the same key.
   A login *without* a `DPoP` header is the web flow and yields an unbound cookie session — DPoP is
   opt-in per session, driven entirely by presenting a proof.
2. **Protected requests** — send `Authorization: DPoP <accessToken>` (never `Bearer` for a bound
   token) plus a fresh proof with `ath`. Mutating requests additionally need the `X-CSRF: 1` header
   (app-wide CSRF defense-in-depth, ADR-0005).
3. **Refresh** — `POST /api/auth/refresh` with `{ "refreshToken": "…" }` in the body and a fresh
   proof **from the same key** (`htm: POST`, `htu: …/api/auth/refresh`, no `ath`). Returns a rotated
   `{ accessToken, refreshToken, refreshTokenExpiresAt }`; both stay bound to the key. ADR-0005
   semantics are unchanged underneath: rotation, reuse-detection, a ~30 s double-fire grace window,
   and sliding expiry that extends on every use.
4. **Logout** — `POST /api/auth/logout` as a bound request (token + proof). Revokes only this
   device's chain.
5. **Change password** — `POST /api/auth/change-password` as a **bound request** (token + a fresh
   proof with `ath`, plus `X-CSRF: 1`) with `{ "currentPassword": "…", "newPassword": "…" }`. This
   revokes **every** session for the user — including this one — and then re-establishes **this
   device** as a new bound chain on the **same key** (possession was just proven on the request, so
   there is no re-key). The response is a JSON body (no cookies):
   `{ accessToken, refreshToken, refreshTokenExpiresAt }`, still `cnf`-bound to your key — adopt the
   returned tokens and discard the old ones. Every other device (and the web cookie session, if any)
   is signed out and must sign in again. A validation failure (wrong `currentPassword`, or a new
   password below policy) returns a `400` validation problem and changes nothing.

## Error vocabulary

Every rejection a client must distinguish, with the **observed** server behavior:

### Login / refresh (token endpoints) — `401` with a typed challenge

```
WWW-Authenticate: DPoP error="invalid_dpop_proof", error_description="<failure>"
```

| `error_description` | Meaning | Recovery |
|---|---|---|
| `StaleProof` | `iat` older than lifetime + skew | regenerate proof, retry once |
| `FutureProof` | `iat` ahead of server clock beyond skew | regenerate proof, retry once; check device clock |
| `ReplayedProof` | `jti` already seen | regenerate proof (fresh `jti`), retry once |
| `MethodMismatch` / `UrlMismatch` | `htm`/`htu` don't match the request | client bug — fix proof construction |
| `InvalidSignature` | signature doesn't verify against the embedded `jwk` | client bug |
| `UnsupportedAlgorithm` | `alg` is not ES256 | client bug |
| `InvalidJwk` | `jwk` missing, malformed, or contains a private key | client bug |
| `MalformedProof` | unparsable JWT, wrong `typ`, missing `iat`/`jti`, or multiple `DPoP` headers | client bug |

If a future server configuration enables RFC 9449 **server-nonce mode**, freshness rejections gain
`error="use_dpop_nonce"` plus a `DPoP-Nonce` response header; adopt the nonce into the next proof.
It is **off** today — `iat`-window freshness only.

### Refresh of a dead chain — `401` with **no** `WWW-Authenticate`

A bare `401` (no DPoP challenge) from `/api/auth/refresh` means the chain itself is gone — revoked
by key mismatch (suspected theft), reuse-detection, logout, Admin-disable, or password change.
**Fall back to a fresh sign-in**, which establishes a new bound session. Note: presenting a refresh
token with a *wrong-key or missing* proof is treated as theft and **revokes the whole chain** — the
legitimate key cannot resurrect it.

### Protected resources — `401` with a DPoP challenge (alongside the `Bearer` one)

| Scenario | Challenge |
|---|---|
| Bound token, no `DPoP` proof header | `DPoP error="invalid_request"` |
| Proof key ≠ token's `cnf.jkt` (stolen token) | `DPoP error="invalid_dpop_proof", error_description="Invalid 'cnf' value."` |
| Stale/future `iat` | `DPoP error="invalid_dpop_proof", error_description="Invalid 'iat' value."` — regenerate and retry |
| Replayed `jti` | `DPoP error="invalid_dpop_proof", error_description="Detected DPoP proof token replay."` |
| Wrong/missing `ath` | `DPoP error="invalid_dpop_proof", error_description="Invalid 'ath' value."` |
| Bound token sent as `Authorization: Bearer` | `Bearer error="invalid_token", error_description="Must use DPoP when using an access token with a 'cnf' claim"` |

On any resource `401` that a regenerated proof doesn't cure, refresh; if refresh answers the bare
`401`, sign in again.

## Telemetry (operator-facing, for completeness)

DPoP rejections surface on the request spans as `auth.dpop.rejected` / `auth.dpop.failure` tags and
a structured log event carrying only the typed failure — never proof or token material.
