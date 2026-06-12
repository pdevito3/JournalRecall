# 0039 — DPoP-bound refresh chain

**Type:** AFK · **Status:** done · **Realizes:** [PRD-0002](../prd/0002-dpop-sender-constrained-bearer-tokens.md) · **Touches:** [ADR-0005](../adr/0005-refresh-token-rotation-and-cookie-hardening.md)

## Parent

[PRD-0002 — DPoP / sender-constrained bearer tokens](../prd/0002-dpop-sender-constrained-bearer-tokens.md)

## What to build

Close the loop with ADR-0005: a stolen refresh token from a bound session cannot mint new access
tokens without the device key.

- **`RefreshToken` entity**: add a nullable `BoundKeyThumbprint`. Null = unbound (web) chain;
  set = DPoP-bound (bearer) chain. The bound key is carried across rotations — a rotated token
  inherits the chain's thumbprint. (Migration regen per project convention: the single
  InitialCreate is regenerated, so drop the dev db before the next run.)
- **`RefreshTokenService`**: `Issue` accepts an optional bound thumbprint. `Rotate` requires the
  presented proof's `jkt` to equal the chain's `BoundKeyThumbprint` for bound chains; a mismatch
  (or missing proof) is treated as **suspected theft** — revoke the whole chain, consistent with
  existing reuse-detection. Unbound chains rotate exactly as today.
- **Login + refresh endpoints**: a DPoP login issues a bound refresh chain alongside the bound
  access token (#0037); `POST /api/auth/refresh` for a bound chain **requires** a valid proof from
  the bound key and mints a new bound access token + rotated bound refresh token. Cookie/web
  refresh is untouched.

## Acceptance criteria

- [x] **Unit (service, mirrors `RefreshTokenServiceTests`):** a bound chain rotates only with the
      matching thumbprint; a mismatched thumbprint revokes the chain; the rotated token inherits
      the binding; unbound chains are unaffected.
- [x] **Integration:** a DPoP login establishes a bound chain; refresh with a proof from the same
      key succeeds and rotates.
- [x] **Integration:** refresh of a bound chain with a **different-key proof** or **no proof** is
      rejected and the chain is revoked.
- [x] **Regression:** ADR-0005 guarantees still hold for bound chains — rotation invalidates the
      prior token, reuse revokes the chain, the grace window works, sliding expiry extends on use.
- [x] **Regression:** unbound (web cookie) refresh behaves exactly as today; existing
      `RefreshTokenTests` pass.

## Blocked by

- #0037
