# 0037 — DPoP bound login: proof in, `cnf`-bound token out, enforced end-to-end

**Type:** AFK · **Status:** done · **Realizes:** [PRD-0002](../prd/0002-dpop-sender-constrained-bearer-tokens.md) · **Touches:** [ADR-0002](../adr/0002-cookie-wrapped-jwt-auth.md)

## Parent

[PRD-0002 — DPoP / sender-constrained bearer tokens](../prd/0002-dpop-sender-constrained-bearer-tokens.md)

## What to build

The tracer bullet: a bearer client that presents a valid **DPoP proof** at login receives an
access token bound to its key (`cnf: { jkt }`), and every protected request must then carry a
fresh proof from that key — while the web cookie flow keeps working with no proof at all.

- **`DPoPProofValidator`** (new deep module, domain-style): given the inbound `DPoP` header, HTTP
  method, and absolute endpoint URL, validate per RFC 9449 §4.3 — well-formed proof JWT,
  `typ: dpop+jwt`, supported alg (ES256 default), signature verified against the **embedded JWK**,
  `htm`/`htu` match, `iat` within the freshness window — and return the canonical JWK SHA-256
  thumbprint (`jkt`) or a typed failure. (`jti` replay detection is deliberately deferred to
  #0038 so this slice has no cache dependency.)
- **Token minting**: the token service accepts an optional bound-key thumbprint; when present the
  minted JWT carries `cnf: { jkt: "<thumbprint>" }`. Cookie/web call sites pass nothing and stay
  unbound.
- **Login endpoint**: when the request carries a `DPoP` header, run the validator and mint a bound
  token; when it doesn't, behave exactly as today. DPoP is per-session opt-in driven by the proof —
  no global mode flag.
- **Resource-server enforcement**: layer the Duende `ConfigureDPoPTokensForScheme(...)` extension
  onto the existing `JwtBearer` scheme with `AllowBearerTokens = true`, so DPoP-bound tokens
  require a matching proof on every protected call while plain bearer/cookie tokens pass
  unchanged. The existing cookie-fallback message handling is untouched.

## Acceptance criteria

- [x] **Unit (validator, mirrors `RefreshTokenServiceTests` style):** a valid proof yields the
      correct, stable `jkt`; tampered signature, stale/future `iat`, wrong `htm`/`htu`, and
      malformed/missing JWK are each rejected with a typed failure.
- [x] **Integration:** login with a valid proof mints a token whose `cnf.jkt` matches the key.
- [x] **Integration:** a protected call with that token and a matching fresh proof succeeds; the
      same call with **no proof** or a **wrong-key proof** returns `401` with a DPoP-appropriate
      `WWW-Authenticate` challenge.
- [x] **Integration:** login without a `DPoP` header behaves exactly as today (unbound token).
- [x] **Regression:** the web cookie flow authenticates with no proof; existing `AuthTests` and
      `CookieHardeningTests` pass unmodified.

## Blocked by

None — can start immediately (ADR #0036 can land in parallel).
