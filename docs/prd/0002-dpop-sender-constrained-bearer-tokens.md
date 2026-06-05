# PRD 0002 — DPoP / sender-constrained bearer tokens

**Status:** ready-for-agent · **Realizes:** a new ADR-0006 (DPoP for the bearer path); supplements
[ADR-0002](../adr/0002-cookie-wrapped-jwt-auth.md) (bearer delivery) and
[ADR-0005](../adr/0005-refresh-token-rotation-and-cookie-hardening.md) (refresh-token rotation)
· **Type:** AFK · **Delivery:** vertical slices (see *Implementation Decisions → Vertical slices*)
· **Blocked by:** issue 0019 (refresh-token rotation) · **Licensing assumption:** the
`Duende.AspNetCore.Authentication.JwtBearer` extension is available to this instance under Duende's
Community Edition (granted) — see *Further Notes*.

> Domain language per [`CONTEXT.md`](../../CONTEXT.md) and the auth model of
> [ADR-0002](../adr/0002-cookie-wrapped-jwt-auth.md): one first-party **JWT**, delivered as an
> **HttpOnly cookie (web)** or a **`Bearer` token (mobile)**, validated identically. This PRD adds
> **sender-constraining** to the *bearer* path only — the web cookie flow is deliberately left
> unchanged.

## Problem Statement

JournalRecall mints one first-party JWT and accepts it as a `Bearer` token from non-browser clients
(today's spec'd mobile path; tomorrow's external-OIDC federation). A bearer token is, by definition,
usable by *anyone who holds it*:

- As the operator of a self-hosted instance, if a mobile device's access or refresh token is ever
  exfiltrated — from device storage, a sync backup, a proxy log, a crash dump — an attacker can
  replay it from their own machine and read or write the victim's journal, and I have no way to tell
  it apart from the legitimate device.
- ADR-0005's rotation + reuse-detection bounds a stolen *refresh* token only at the moment the
  legitimate device next refreshes; in between, and for a token whose victim never returns, theft is
  unbounded. That residual risk was explicitly accepted for the cookie flow (HttpOnly mitigates it),
  but the bearer path has no equivalent mitigation.
- The web cookie flow is already well-protected (HttpOnly delivery means the token is never exposed
  to JavaScript), so the gap is specifically the bearer path that mobile and external OIDC will use.

The desired property: a stolen bearer token — access *or* refresh — should be **useless to anyone
who does not also hold the device's private key**.

## Solution

Adopt **DPoP (Demonstrating Proof-of-Possession, RFC 9449)** for the bearer path, binding tokens to
a public/private key pair the client holds in device-backed secure storage:

- On a **bearer login**, the client presents a **DPoP proof** (a short-lived JWT it signs with its
  private key). JournalRecall validates the proof, computes the public key's thumbprint, and **binds
  the minted access token to that key** via a `cnf: { jkt }` confirmation claim.
- The **refresh-token chain is bound to the same key** (extending ADR-0005): refreshing requires a
  proof from the original device key, so a stolen refresh token cannot mint new access tokens on its
  own.
- On **every protected API call**, the client sends a fresh proof; the resource server checks that
  the proof's key matches the token's `cnf.jkt`. A replayed token without the matching private key is
  rejected.
- The **web cookie flow is untouched** — it continues to present an unconstrained bearer token via
  the HttpOnly cookie, and the resource server accepts both (DPoP-bound and plain-bearer) so the two
  client classes coexist during and after rollout.

The security-critical **resource-server validation** is handled by the proven
`Duende.AspNetCore.Authentication.JwtBearer` extension layered onto the existing `JwtBearer` scheme;
the **token-endpoint binding** (validating the proof at login/refresh and stamping `cnf`) is small,
RFC-bounded first-party code, consistent with ADR-0002/0005 keeping JournalRecall its own issuer.

## User Stories

### Sender-constrained access (the core property)

1. As the operator of a self-hosted instance, I want a mobile client's access token bound to a device
   key, so that a token copied off the device is useless from any other machine.
2. As a mobile client, I want to present a signed DPoP proof when I sign in, so that the token I
   receive is cryptographically bound to my private key.
3. As a mobile client, I want every protected request to carry a fresh, short-lived proof, so that a
   captured proof can't be replayed later.
4. As the resource server, I want to reject any request whose proof key doesn't match the token's
   `cnf.jkt`, so that a stolen token presented without the key fails.
5. As the resource server, I want to reject a replayed proof (same `jti` seen twice within its
   lifetime), so that a captured request can't be resent.
6. As the resource server, I want to reject a proof whose `htm`/`htu` don't match the actual method
   and URL, or whose `iat` is outside the freshness window, so that proofs can't be reused across
   endpoints or over time.

### Sender-constrained refresh (closing the loop with ADR-0005)

7. As the operator, I want the refresh-token chain bound to the same device key as the access token,
   so that a stolen refresh token can't mint fresh access tokens on its own.
8. As a mobile client, I want `/api/auth/refresh` to require a proof from my original key, so that
   only my device can extend my session.
9. As the resource server, I want a refresh presented with a proof from a *different* key to be
   rejected (and treated as suspicious, consistent with reuse-detection), so that refresh theft is
   caught.
10. As a mobile client, I want refresh to keep the ADR-0005 rotation, reuse-detection, grace window,
    and sliding-expiry semantics, so that DPoP layers on top of durable sessions rather than
    replacing them.

### Coexistence with the web cookie flow

11. As a web (browser) User, I want the cookie flow to keep working exactly as today with no proof
    required, so that DPoP doesn't degrade the browser experience it provides no benefit to.
12. As the resource server, I want to accept both DPoP-bound bearer tokens and plain bearer/cookie
    tokens on the same endpoints, so that browser and mobile clients share one validation path.
13. As the operator, I want the web cookie token to remain *unbound* by design, so that no private key
    has to live in browser-accessible storage where same-origin script could use it anyway.

### Onboarding & key lifecycle (mobile)

14. As a mobile client, I want to generate my key pair in device-backed secure storage (Secure
    Enclave / Android Keystore) once, so that the private key never leaves the device.
15. As a mobile client, I want a clear, documented proof contract (headers, claims, signing
    algorithm, error responses), so that I can implement proof generation without guesswork.
16. As a mobile client whose proof is rejected for staleness, I want a well-defined error I can detect,
    so that I can regenerate a proof and retry (and, if server-nonce mode is ever enabled, adopt the
    server's nonce).
17. As a mobile client that reinstalls or rotates its key, I want signing in again with a new key to
    establish a new bound session, so that key loss is recoverable by re-authenticating.

### Operability & safety

18. As the operator, I want DPoP rejections to surface in the existing auth telemetry, so that I can
    see proof failures, replays, and key mismatches without exposing token contents.
19. As the operator, I want the proof-replay cache to work on a single-instance self-hosted deployment
    with no extra infrastructure, so that I don't have to run Redis for a home-lab install.
20. As the operator, I want all existing auth guarantees — the Privacy invariant, Admin-disable and
    password-change revocation, logout-this-device — to remain intact with DPoP enabled, so that
    sender-constraining adds protection without removing any.

## Implementation Decisions

### Architecture & ADRs

- **Scope is the bearer path only.** The web cookie flow (ADR-0002) is intentionally left
  unconstrained: the token is HttpOnly and never exposed to script, so DPoP adds no meaningful
  protection there, and a browser-held private key would be usable by any same-origin XSS anyway.
  This matches PRD-0001's framing that "DPoP is overkill for the cookie web flow."
- **A new ADR-0006** records the decision: adopt DPoP for bearer clients; use the
  `Duende.AspNetCore.Authentication.JwtBearer` extension for *resource-server* proof validation;
  keep token-endpoint binding as first-party code; bind both access tokens and the refresh chain.
  ADR-0002 gets a one-line amendment (bearer path is now sender-constrainable) and ADR-0005 a
  one-line amendment (the refresh chain may be DPoP-bound).
- **Two halves, two owners.** Resource-server validation (per-request proof checking) is the proven
  library's job; token-endpoint binding (accepting a proof at login/refresh, computing the
  thumbprint, stamping `cnf`) has no drop-in for a first-party issuer and is our small RFC-9449 §4.3
  code. This split is the whole reason the library is worth adopting — it owns the gnarly half.

### Modules (deep modules favored for isolated testing)

- **`DPoPProofValidator`** *(deep module, new)* — the encapsulated token-endpoint proof check. Input:
  the inbound `DPoP` header, the HTTP method, and the absolute endpoint URL. Validates per RFC 9449
  §4.3 — well-formed proof JWT, `typ: dpop+jwt`, supported signing alg, signature verified against the
  **embedded JWK**, `htm`/`htu` match the token endpoint, `iat` within the freshness window, `jti`
  not already seen (replay) — and returns the canonical **JWK SHA-256 thumbprint (`jkt`)** on success
  or a typed failure. Simple interface, dense logic, RFC-bounded → rarely changes. Used at the login
  and refresh endpoints (the resource-request side is the library's, not this module's).
- **`JwtTokenService` extension** *(modify existing)* — add an optional `string? dpopKeyThumbprint`
  parameter to `Create(...)`; when present, embed the confirmation claim `cnf` as `{ "jkt":
  "<thumbprint>" }`. No change to the cookie/web call sites (they pass nothing → unbound token, as
  today).
- **Resource-server DPoP validation** *(library wiring in `AuthRegistration`)* — layer
  `ConfigureDPoPTokensForScheme(scheme, …)` onto the existing `JwtBearer` registration with
  `AllowBearerTokens = true` (browser/cookie tokens stay valid as plain bearer) and
  `EnableReplayDetection = true`. The handler enforces the proof↔`cnf.jkt` match on every protected
  request. No change to the existing `OnMessageReceived` cookie fallback.
- **`RefreshToken` binding** *(modify existing entity + EF mapping)* — add a nullable
  `BoundKeyThumbprint` column to the refresh-token chain. Null = an unbound (web) chain; set = a
  DPoP-bound (bearer) chain. Carried across rotations (a rotated token inherits the chain's bound
  key).
- **`RefreshTokenService` extension** *(modify existing deep module)* — `Issue` accepts an optional
  bound thumbprint; `Rotate` requires the presented proof's `jkt` to equal the chain's
  `BoundKeyThumbprint` for bound chains, and a mismatch is treated as suspected theft (revoke the
  chain, consistent with existing reuse-detection). Unbound chains rotate exactly as today.
- **Token-endpoint wiring** *(modify login + refresh endpoints)* — when a request carries a `DPoP`
  header, run `DPoPProofValidator`, mint a `cnf`-bound access token, and issue/rotate a bound refresh
  chain. When it does not (cookie/web), behave exactly as today. This makes DPoP **per-session
  opt-in, driven by the client presenting a proof** — no global mode flag.
- **Proof-replay cache** *(infrastructure seam)* — the library's replay detection and
  `DPoPProofValidator`'s `jti` check share an `IDistributedCache`/`HybridCache`. Default to the
  **in-memory** HybridCache so a single-instance self-hosted deployment needs no Redis; a Redis
  backing is a config swap for any future multi-instance deployment. Entries live for the proof
  lifetime plus clock-skew margin.

### Reference client flow (mobile, documented spec — not app code)

A documented contract the future iOS app (separate app/repo) implements against; this PRD specifies
it but does not ship Swift:

- **Key pair**: generated once in the **Secure Enclave** (iOS) / **Android Keystore**, non-exportable;
  the private key never leaves the device. Default curve/alg per RFC 9449 (e.g. ES256).
- **Per-request proof**: a JWT with header `{ typ: "dpop+jwt", alg, jwk: <public key> }` and claims
  `{ htm, htu, iat, jti }`, signed by the private key, sent in the `DPoP` request header. A new proof
  is minted per request (proofs are short-lived and endpoint-specific).
- **Login**: send a proof to the login endpoint → receive a `cnf`-bound access token + a bound refresh
  chain.
- **Refresh**: send a proof from the same key to `/api/auth/refresh`.
- **Error handling**: on a freshness/`nonce` rejection, regenerate and retry; on a key-mismatch or
  chain-revoked response, fall back to a fresh sign-in (re-establishes a new bound session).

### API contracts (shapes, not paths-as-truth)

- **Login (bearer)** — accepts an optional `DPoP` proof header. With a valid proof → access token
  carries `cnf.jkt`, refresh chain bound to that key. Without → unconstrained token (today's
  behavior).
- **`POST /api/auth/refresh`** — for a bound chain, **requires** a `DPoP` proof whose `jkt` matches
  the chain; mismatch/absent → reject and revoke the chain (suspected theft). Unbound chains refresh
  as today.
- **Protected resource endpoints** — a DPoP-bound token must arrive with a matching proof (enforced
  by the library); a plain bearer/cookie token is accepted without a proof. Proof failures → `401`
  with a DPoP-appropriate `WWW-Authenticate` challenge.
- **Confirmation claim** — minted access tokens for bound sessions carry `cnf: { jkt: "<thumbprint>" }`
  per RFC 9449 §6.

### What is explicitly NOT changed

- The web SPA, the HttpOnly cookie delivery, the `__Host-`/`__Secure-` cookie hardening, `X-CSRF`, and
  the single-flight refresh interceptor (PRD-0001 / ADR-0005) are untouched.
- The single-JWT model (ADR-0002) is unchanged — one token, validated identically; DPoP is an
  additive constraint on the bearer presentation, not a second token type.

## Testing Decisions

A good test asserts **external behavior** — HTTP status, the `WWW-Authenticate` challenge, whether a
token is accepted or rejected, whether a session survives — never internal call sequences or the
library's internals. Prior art: `tests/JournalRecall.Api.Tests/AuthTests.cs`,
`CookieHardeningTests.cs`, `RefreshTokenTests.cs` (endpoint integration via `WebApplicationFactory` +
Shouldly/xUnit), and `tests/JournalRecall.Api.Tests/Domain/RefreshTokenServiceTests.cs` (isolated
domain unit tests). Integration clients must use an **https base address** (Secure cookie prefixes).

Modules to test (all four confirmed):

- **`DPoPProofValidator`** *(isolated unit tests, domain-style — mirrors `RefreshTokenServiceTests`)*
  — a valid proof yields the correct, stable `jkt` thumbprint; a tampered signature is rejected; a
  stale (or future) `iat` is rejected; an `htm`/`htu` that doesn't match the endpoint is rejected; a
  replayed `jti` (second presentation within lifetime) is rejected; a malformed or missing JWK is
  rejected. The deepest new logic → the densest coverage.
- **Bearer DPoP flow** *(integration)* — login with a valid proof mints a token whose `cnf.jkt` matches
  the key; a protected call with a matching fresh proof succeeds; the same call with **no proof**,
  with a **wrong-key proof**, and with a **replayed proof** each returns `401`. Confirms the
  resource-server constraint end-to-end.
- **Web cookie regression** *(integration)* — the browser/cookie flow continues to authenticate with
  **no proof** (`AllowBearerTokens`), and existing `AuthTests`/`CookieHardeningTests` behavior is
  unaffected. Guards against DPoP accidentally constraining the web path.
- **Refresh-token binding** *(integration)* — a bound refresh chain rotates only when presented with a
  proof from its bound key; a refresh attempt with a **different-key** proof (or none) is rejected and
  the chain is revoked; the ADR-0005 rotation/reuse-detection/grace-window guarantees still hold for
  the bound chain.

## Out of Scope

- **DPoP on the web cookie flow** — deliberately excluded; HttpOnly already mitigates token theft for
  browsers and a browser-held key buys nothing against same-origin XSS. The cookie token stays
  unbound.
- **PKCE** — applies to the external-OIDC authorization-code flow, not the first-party bearer login;
  it remains deferred and belongs with the external-OIDC work, not this PRD.
- **Server-issued nonce mode** (`ProofTokenExpirationMode = Nonce`) — the library supports it (extra
  freshness via a server round-trip), but it adds latency and state beyond this threat model; left as
  a config toggle to enable later if needed, not wired on by default.
- **Redis / multi-instance replay cache** — the default in-memory HybridCache covers single-instance
  self-hosting; a distributed cache is a configuration swap for a future scaled deployment, not built
  here.
- **The iOS app implementation** — the Swift/Secure-Enclave client is a separate app/repo. This PRD
  delivers the *server* contract plus the documented *reference proof flow*; it does not ship client
  code.
- **A user-facing device/session management UI** — out of scope as in PRD-0001; the bound-key column
  leaves room for it later.

## Further Notes

- **Licensing assumption.** `Duende.AspNetCore.Authentication.JwtBearer` is source-available and
  requires a license for production use; this PRD assumes the **free Community Edition** applies to
  this self-hosted instance (granted). If JournalRecall is ever *redistributed* for others to
  self-host, Duende's redistribution terms apply independently — at that point the resource-server
  validation half could instead be hand-rolled against RFC 9449 §4.3 (the same logic
  `DPoPProofValidator` already encodes for the token endpoint) to keep the stack license-clean. The
  module boundary is drawn so that swap is contained.
- **Why bind both access and refresh.** Binding only the access token would still let a stolen refresh
  token mint fresh (bound-to-the-attacker… no — bound-to-the-original-key) access tokens; without
  also constraining refresh, a leaked refresh token remains a standalone credential. Binding the chain
  to the device key makes the *entire* bearer session sender-constrained, which is the property the
  Problem Statement asks for.
- **Coexistence is the rollout strategy.** `AllowBearerTokens = true` means there is no flag-day:
  browsers keep using unbound cookie tokens, mobile presents proofs and gets bound tokens, and both
  validate on the same endpoints. DPoP is opt-in per session, driven entirely by whether the client
  presents a proof.
- **Relationship to ADR-0005's accepted residual risk.** ADR-0005 accepted "a silently stolen token
  whose victim never returns" as unbounded for the cookie path. For the *bearer* path this PRD closes
  exactly that gap: a silently stolen bearer token (access or refresh) is inert without the device
  key, so it no longer depends on the victim returning to trigger reuse-detection.
