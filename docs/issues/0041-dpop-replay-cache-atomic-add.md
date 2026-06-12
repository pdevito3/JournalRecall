# 0041 — DPoP replay cache: atomic add, app-owned key, real-implementation coverage

**Type:** AFK · **Status:** done · **Realizes:** [PRD-0002](../prd/0002-dpop-sender-constrained-bearer-tokens.md) · **Touches:** [ADR-0014](../adr/0014-dpop-sender-constrained-bearer-path.md), #0038

## Parent

[PRD-0002 — DPoP / sender-constrained bearer tokens](../prd/0002-dpop-sender-constrained-bearer-tokens.md)
(follow-up from code review of #0038)

## What to build

The proof-replay cache currently probes then sets: two concurrent presentations of the same `jti`
both observe "unseen" and both pass. Because ADR-0005's grace window deliberately tolerates
re-presentation of a just-rotated refresh token, the `jti` check is the *only* barrier against a
captured refresh request replayed concurrently — and it races. The cache is also resolved through
the Duende library's DI key while sharing no entries with the library's half, coupling the
deliberately Duende-free first-party half to the library for nothing.

- Make the first-party replay add **atomic**: a single winner among concurrent presentations of the
  same `jti`. HybridCache's stampede protection gives this directly — the factory returns a
  per-caller marker and only the caller whose marker was stored wins.
- Register the replay HybridCache under an **app-owned key**, and point the Duende library's keyed
  registration at the same backing in one place in the auth registration, so the "one cache, swap
  once for Redis" property survives without the first-party half importing Duende.
- Cover the **real** `HybridDPoPReplayCache` directly (today only an in-memory fake proves expiry
  semantics): first add wins, second within lifetime loses, and a re-add after entry expiry wins.

## Acceptance criteria

- [x] **Unit/Integration (real cache):** concurrent `TryAddAsync` calls with one `jti` produce
      exactly one `true` (assert under `Task.WhenAll` with a realistic degree of parallelism).
- [x] **Unit/Integration (real cache):** a second sequential add within the lifetime returns false;
      an add after the entry's expiration returns true.
- [x] `DPoPReplayCache.cs` (and the rest of the first-party half) no longer references the
      `Duende.*` namespace; the Duende resource-server half still resolves a cache and its replay
      detection still passes the existing functional replay test.
- [x] All existing DPoP unit and functional tests pass unmodified (`dpop_proof_validator_tests`,
      `dpop_tests`, `dpop_error_contract_tests`, `dpop_guarantee_tests`).

## Blocked by

None — can start immediately.
