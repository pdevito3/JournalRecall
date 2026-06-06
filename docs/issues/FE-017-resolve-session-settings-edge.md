# FE-017 — Resolve the Session→settings cross-feature import

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

The **Session** timeline imports the **settings** feature's hooks directly (`useSettings` /
`useUpdateSettings`, for the timezone picker and the location opt-in toggle) — a direct feature→feature
reach. Resolve it so there is no feature→feature edge before the import-boundary lint lands (FE-019).

**Decision (record in the issue/PR), per the PRD's "promote to shared" criterion (used by ≥2 features
and domain-agnostic → `shared`, else keep as its own vertical):** choose one of —
- promote settings access to `shared` config (recommended — timezone/location are domain-agnostic),
- pass settings into the timeline as props from the route, or
- sanction it as an explicitly allowed composition edge in the lint config.

## Acceptance criteria

- [ ] The timeline no longer imports the settings feature directly; the chosen resolution is applied.
- [ ] The decision and its rationale (against the promote-to-shared criterion) are recorded in the PR.
- [ ] App behavior (timezone picker, location toggle) is unchanged.

## Blocked by

- None - can start immediately
