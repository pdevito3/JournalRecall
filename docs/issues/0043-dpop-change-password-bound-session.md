# 0043 — Change-password from a bound session re-establishes a bound session

**Type:** AFK · **Status:** done · **Realizes:** [PRD-0002](../prd/0002-dpop-sender-constrained-bearer-tokens.md) · **Touches:** [ADR-0014](../adr/0014-dpop-sender-constrained-bearer-path.md), #0039, #0040

## Parent

[PRD-0002 — DPoP / sender-constrained bearer tokens](../prd/0002-dpop-sender-constrained-bearer-tokens.md)
(follow-up from code review of #0039/#0040)

## What to build

`POST /api/auth/change-password` revokes every session and re-establishes the calling device — but
always as an **unbound cookie** session with a `204` body. A DPoP-bound bearer client calling it is
stranded: its bound chain is revoked, the replacement tokens land in cookies it will never use, its
binding is silently dropped, and a live unbound refresh chain is orphaned server-side.

Mirror the login split: when the caller is a bound session (the authenticated access token carries
`cnf`), re-establish **this device** as a bound chain — same key, since possession was just proven
on this request — and return the tokens in the body (`TokenResponse`), setting no cookies. The web
cookie flow is untouched. Document the change-password recovery in the mobile proof contract
(`docs/mobile/dpop-proof-contract.md`), which currently doesn't cover this endpoint.

## Acceptance criteria

- [x] **Integration:** a bound client changes its password (token + fresh proof) → `200` with a
      body `TokenResponse`; the new access token carries the same `cnf.jkt`; no auth cookies are
      set; the new refresh token rotates with a proof from the same key.
- [x] **Integration:** after the change, the user's *other* sessions (web cookie and/or
      other-key bound chains) are revoked, and the old bound chain no longer refreshes — the
      existing revoke-all guarantee holds.
- [x] **Integration:** no orphaned unbound chain is minted for a bound caller (the only live chain
      after the call is the bound one returned in the body).
- [x] **Regression:** the web cookie change-password flow behaves exactly as today; existing
      `forced_password_change_tests` and `dpop_guarantee_tests` pass unmodified.
- [x] The proof-contract doc gains a change-password section covering the request shape and the
      recovery semantics, consistent with the documented error vocabulary.

## Blocked by

- #0042 (reshapes the same endpoint file's DPoP handling; land the refresh hardening first)
