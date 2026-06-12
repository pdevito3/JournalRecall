# 0044 — Close the DPoP enforcement test gaps

**Type:** AFK · **Status:** done · **Realizes:** [PRD-0002](../prd/0002-dpop-sender-constrained-bearer-tokens.md) · **Touches:** #0037, #0040

## Parent

[PRD-0002 — DPoP / sender-constrained bearer tokens](../prd/0002-dpop-sender-constrained-bearer-tokens.md)
(follow-up from code review of #0037/#0040)

## What to build

Three enforcement paths exist in code with zero coverage — a regression in any of them would pass
the entire suite today:

- **Unsupported algorithm.** The validator rejects non-ES256 proofs, but `TestDPoPKey` can only
  sign ES256, so the path is untestable as-is. Give the helper a signing-algorithm/key knob (e.g.
  an RS256 variant) and cover the rejection.
- **Multiple `DPoP` headers.** RFC 9449's "exactly one header" rule is enforced at the endpoints
  (`headers.Count != 1` → `MalformedProof`) but never exercised over HTTP.
- **`bearer_downgrade` telemetry.** A `cnf`-bound token presented as plain Bearer sets the
  `auth.dpop.failure = "bearer_downgrade"` span tag; the tag path is entirely unproven (the
  existing telemetry test covers only `StaleProof` and `invalid_dpop_proof`).

Tests only — no production behavior changes. Follow the existing layering: validator behavior in
`dpop_proof_validator_tests` (unit), HTTP status/challenge contracts and telemetry in the
functional DPoP suites.

## Acceptance criteria

- [x] **Unit:** a proof signed with a non-ES256 algorithm is rejected with the
      `UnsupportedAlgorithm` typed failure (via a new `TestDPoPKey` knob, not a hand-rolled JWT).
- [x] **Integration:** a login (or refresh) request carrying two `DPoP` headers returns `401` with
      the `MalformedProof` DPoP challenge.
- [x] **Integration:** presenting a bound token as plain `Bearer` produces an exported span tagged
      `auth.dpop.rejected = true` and `auth.dpop.failure = "bearer_downgrade"`, with no proof or
      token material in any tag value.
- [x] Full suite passes; no production code modified (test and SharedTestHelpers changes only).

## Blocked by

None — can start immediately.
