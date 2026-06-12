# DPoP sender-constraining for the bearer path

## Status

accepted — realizes [PRD-0002](../prd/0002-dpop-sender-constrained-bearer-tokens.md); supplements
[ADR-0002](0002-cookie-wrapped-jwt-auth.md) (bearer delivery) and
[ADR-0005](0005-refresh-token-rotation-and-cookie-hardening.md) (refresh-token rotation). The
PRD's "ADR-0006" reference predates ADRs 0006–0013 and resolves to this document.

## Context & decision

ADR-0002's single first-party JWT is accepted as a `Bearer` token from non-browser clients. A
bearer token is usable by anyone who holds it: exfiltrated from device storage, a backup, a proxy
log, or a crash dump, it can be replayed from any machine, and ADR-0005's reuse-detection bounds a
stolen *refresh* token only when the legitimate device next refreshes. We adopt **DPoP
(RFC 9449)** for the **bearer path only**, so that a stolen bearer token — access *or* refresh —
is useless without the device's private key:

1. **Bound login.** A bearer client presents a **DPoP proof** (a short-lived JWT signed by a key
   in device-backed secure storage, ES256 default) at login; the minted access token carries the
   key's thumbprint in a `cnf: { jkt }` confirmation claim (RFC 9449 §6). DPoP is **per-session
   opt-in, driven by the proof** — no global mode flag.
2. **Two halves, two owners.** *Resource-server* proof validation (the per-request proof↔`cnf.jkt`
   match) is the proven `Duende.AspNetCore.Authentication.JwtBearer` extension, layered onto the
   existing `JwtBearer` scheme with bearer tokens still allowed, so DPoP-bound and plain
   bearer/cookie tokens coexist on the same endpoints — that coexistence *is* the rollout
   strategy. *Token-endpoint* binding (proof validation at login/refresh, thumbprint computation,
   `cnf` stamping) has no drop-in for a first-party issuer and is our small RFC-9449 §4.3 code
   (`DPoPProofValidator`).
3. **The refresh chain is bound to the same key** (extending ADR-0005): `RefreshToken` gains a
   nullable `BoundKeyThumbprint`, carried across rotations. Rotating a bound chain requires a
   proof from the bound key; a mismatch (or missing proof) is suspected theft and revokes the
   chain, consistent with existing reuse-detection. Binding only the access token would leave a
   stolen refresh token a standalone credential — binding the chain makes the *entire* bearer
   session sender-constrained.
4. **The web cookie flow stays deliberately unbound.** The HttpOnly cookie already keeps the token
   away from script, and a browser-held private key would be usable by any same-origin XSS anyway
   — DPoP buys the cookie path nothing. Cookie call sites pass no thumbprint and mint unbound
   tokens, exactly as today.
5. **Proof-replay protection defaults to an in-memory cache** (the `HybridCache` /
   `IDistributedCache` seam shared by the validator's `jti` check and the library's replay
   detection), so a single-instance self-hosted deployment needs no Redis; a distributed backing
   is a configuration swap, not a code change.

## Considered options

- **DPoP everywhere, including the web cookie flow** — rejected: HttpOnly delivery already
  mitigates browser token theft, and the private key would have to live where same-origin script
  can use it, defeating the point.
- **Hand-roll resource-server validation too** — rejected while the Duende Community Edition
  applies: the per-request half is the gnarly, attack-facing half and the library is proven.
- **Mutual-TLS sender-constraining (RFC 8705)** — rejected: client certificates are operationally
  hostile to mobile and to reverse-proxied self-hosted deployments; DPoP works at the application
  layer.
- **Binding the access token only** — rejected: leaves the refresh token a bearer credential and
  reopens exactly the gap PRD-0002 set out to close.

## Consequences

- A silently stolen bearer token no longer depends on the victim returning to trigger
  reuse-detection — it is inert without the device key. ADR-0005's accepted residual risk now
  applies only to the cookie path, where HttpOnly bounds it.
- **Licensing assumption:** `Duende.AspNetCore.Authentication.JwtBearer` is used under Duende's
  free **Community Edition** (granted for this self-hosted instance). If JournalRecall is ever
  redistributed, Duende's terms apply independently; the module split (validator first-party,
  resource-server half behind the library's registration) keeps a hand-rolled swap contained.
- The replay cache is in-memory: replay protection is per-instance and resets on restart. Both are
  acceptable for a single-instance deployment within the proof freshness window; multi-instance
  deployments must swap in a distributed cache.
- Server-issued nonce mode stays off (extra freshness at the cost of latency and state); it
  remains a configuration toggle, not a default.
- Mobile clients must implement the documented proof contract (fresh proof per request); key
  loss or rotation is recovered by signing in again with the new key, which establishes a new
  bound session.
