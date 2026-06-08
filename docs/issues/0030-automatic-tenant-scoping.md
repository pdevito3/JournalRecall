# 0030 — Automatic tenant scoping via `ITenantScoped`

**Type:** AFK · **Status:** ready-for-agent · **Realizes:** [PRD-0007](../prd/0007-session-activity-metadata.md), [ADR-0012](../adr/0012-per-entity-config-and-automatic-tenant-scoping.md) · **Touches:** [ADR-0002](../adr/0002-cookie-wrapped-jwt-auth.md), [ADR-0005](../adr/0005-refresh-token-rotation-and-cookie-hardening.md)

## Parent

[PRD-0007 — Session Activity metadata](../prd/0007-session-activity-metadata.md)

## What to build

Apply tenant scoping (the **Privacy invariant**) automatically to any entity that opts in via a
marker, so a new tenant-scoped entity can't silently miss its filter. No user-facing change —
behavior-preserving for existing entities. Independent of #0028/#0029.

- Introduce an **`ITenantScoped`** marker interface (exposes `UserId`). `Session`, `Summary`,
  `Person`, and `Correction` implement it; **`RefreshToken` does not** (it owns a `UserId` but must
  stay unscoped so rotation works with no current user established — ADR-0005); the app-wide settings
  entities have no `UserId` and are naturally out.
- A **strongly-typed generic helper** applies the named `TenantFilter` (`e => e.UserId ==
  _currentUserId`) and is invoked over every `ITenantScoped` type in the model, replacing the four
  hand-written `HasQueryFilter` lines in one atomic change.
- **Footgun to avoid (the rationale):** the filter must reference the DbContext instance field
  `_currentUserId` so EF parameterizes and re-evaluates it **per query**. Capturing the *value* at
  model-build time would bake one User's id into EF's cached compiled model and leak rows across
  tenants. Keep the helper strongly-typed — reflection selects *which* entities, never rebuilds the
  predicate.

## Acceptance criteria

- [ ] `Session`, `Summary`, `Person`, and `Correction` are tenant-scoped through the marker-driven
      helper; the four manual `HasQueryFilter` lines are gone.
- [ ] `RefreshToken` is **not** tenant-filtered, and token rotation still works after the access token
      has expired.
- [ ] Adding `ITenantScoped` to a new entity scopes it with no other wiring.
- [ ] The tenant filter still re-evaluates per query (no cross-tenant leakage via a cached model).
- [ ] **Integration test**: an `ITenantScoped` entity created by one User is invisible to another; a
      regression guard asserts `RefreshToken` remains unfiltered.

## Blocked by

- None — can start immediately (independent of #0028/#0029).
