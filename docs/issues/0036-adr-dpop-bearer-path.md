# 0036 — ADR: DPoP sender-constraining for the bearer path

**Type:** AFK · **Status:** done · **Realizes:** [PRD-0002](../prd/0002-dpop-sender-constrained-bearer-tokens.md) · **Touches:** [ADR-0002](../adr/0002-cookie-wrapped-jwt-auth.md), [ADR-0005](../adr/0005-refresh-token-rotation-and-cookie-hardening.md)

## Parent

[PRD-0002 — DPoP / sender-constrained bearer tokens](../prd/0002-dpop-sender-constrained-bearer-tokens.md)

## What to build

Record the approved decision as a new ADR (next number — **ADR-0014**; the PRD's "ADR-0006"
reference predates ADRs 0006–0013 and is stale): adopt **DPoP (RFC 9449)** for the bearer path
only; use the `Duende.AspNetCore.Authentication.JwtBearer` extension for *resource-server* proof
validation (Community Edition, granted); keep *token-endpoint* binding (proof validation at
login/refresh, `cnf.jkt` stamping) as small first-party RFC-9449 §4.3 code; bind both the access
token and the refresh chain to the device key; leave the web cookie flow deliberately unbound.

Capture the rationale from the PRD: the two-halves/two-owners split, why the cookie path gains
nothing from DPoP, coexistence via `AllowBearerTokens` as the rollout strategy, and the licensing
boundary (the module split keeps a hand-rolled swap contained if redistribution terms ever bite).

Amend existing ADRs in place, one line each: ADR-0002 (the bearer path is now
sender-constrainable) and ADR-0005 (the refresh chain may be DPoP-bound).

## Acceptance criteria

- [x] New ADR-0014 exists in `docs/adr/`, following the house ADR format, covering: decision,
      library-vs-first-party split, access **and** refresh binding, web-cookie exclusion, replay
      cache default (in-memory, single-instance), and the licensing assumption.
- [x] ADR-0002 carries a one-line amendment pointing at ADR-0014.
- [x] ADR-0005 carries a one-line amendment pointing at ADR-0014.
- [x] No code changes.

## Blocked by

None — can start immediately.
