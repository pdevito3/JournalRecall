# 0038 — DPoP proof replay protection

**Type:** AFK · **Status:** done · **Realizes:** [PRD-0002](../prd/0002-dpop-sender-constrained-bearer-tokens.md)

## Parent

[PRD-0002 — DPoP / sender-constrained bearer tokens](../prd/0002-dpop-sender-constrained-bearer-tokens.md)

## What to build

A captured DPoP proof cannot be presented twice. Add the replay seam that #0037 deferred:

- A shared **proof-replay cache** (the `IDistributedCache`/`HybridCache` seam) used by both halves:
  `DPoPProofValidator` gains its `jti` check (second presentation of the same `jti` within the
  proof lifetime → typed replay failure), and the resource-server library wiring enables
  `EnableReplayDetection = true` against the same cache.
- Default to the **in-memory** HybridCache so a single-instance self-hosted deployment needs no
  Redis; a distributed backing is a configuration swap, not built here. Entries live for the proof
  lifetime plus a clock-skew margin.

## Acceptance criteria

- [x] **Unit (validator):** presenting the same `jti` twice within its lifetime is rejected; a
      fresh `jti` after expiry of the prior entry is accepted.
- [x] **Integration:** a replayed proof is rejected at the **login endpoint** (`401`).
- [x] **Integration:** a replayed proof is rejected at a **protected resource endpoint** (`401`
      with a DPoP-appropriate `WWW-Authenticate` challenge).
- [x] The app boots and all replay tests pass with no external cache infrastructure (in-memory
      default).

## Blocked by

- #0037
