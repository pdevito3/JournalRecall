# Per-entity EF configuration, with tenant scoping applied automatically via a marker

## Status

accepted — realized by [PRD-0007](../prd/0007-session-activity-metadata.md); refines the enforcement
of the **Privacy invariant** established in [ADR-0002](0002-cookie-wrapped-jwt-auth.md) and the
`RefreshToken` exception in [ADR-0005](0005-refresh-token-rotation-and-cookie-hardening.md)

## Context & decision

Entity persistence configuration lived inline in one large `OnModelCreating`, and the **Privacy
invariant** — the single most important property of this app — was enforced by a `HasQueryFilter(s =>
s.UserId == _currentUserId)` line **hand-written on every tenant-scoped entity**. Scoping a new entity
correctly depended on a developer remembering to copy that line.

Two decisions:

1. **Static mapping moves into per-entity `IEntityTypeConfiguration<T>` classes**, auto-registered via
   `ApplyConfigurationsFromAssembly`. Mapping becomes discoverable and isolation-testable. Realized for
   `Session` first; other entities migrate opportunistically.
2. **Tenant scoping is applied automatically to any entity that opts in via an `ITenantScoped`
   marker.** A strongly-typed generic helper applies the named `TenantFilter` and is invoked over every
   `ITenantScoped` type in the model, replacing the hand-written filters.

Scoping keys off the **marker, not the presence of a `UserId` column**, because the two diverge:
`RefreshToken` *has* a `UserId` but **must stay unscoped** (rotation runs after the access token has
expired, with no current user established, so a filter would hide the very row being rotated —
ADR-0005). Opt-in by marker makes that exclusion explicit instead of an exception to a property-name
rule.

## Considered options

- **Keep hand-wired per-entity filters.** Rejected: a forgotten line is a silent
  cross-tenant data leak.
- **Filter every entity that has a `UserId`.** Rejected: would wrongly scope `RefreshToken` and break
  rotation; needs exceptions anyway.
- **Configs that take the current user via constructor.** Rejected: incompatible with
  `ApplyConfigurationsFromAssembly` (it can't inject), which defeats the auto-discovery being sought.

## Consequences

- A new tenant-scoped entity is protected **only if it implements `ITenantScoped`.** The marker is a
  **privacy-critical opt-in**, documented here so a future entity author knows to add it. Forgetting it
  means no scoping — automation makes scoping *consistent*, not automatic-by-default.
- The instance-dependent `HasQueryFilter` stays in `OnModelCreating` (it needs `_currentUserId`); only
  the **static** mapping lives in the configuration classes.
- **Footgun, deliberately avoided:** the filter must reference the DbContext instance field
  `_currentUserId` so EF parameterizes it and **re-evaluates per query**. Capturing the *value* at
  model-build time would bake one User's id into EF's **cached compiled model** and leak rows across
  every other tenant — the worst bug this app can have. The helper is therefore **strongly-typed**,
  preserving the exact closure semantics of the original per-entity filters; reflection only selects
  *which* entities, never rebuilds the predicate.
