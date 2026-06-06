# FE-019 — ESLint flat config + import-boundary rule (error) + `lint` script & CI

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

The web project has no ESLint today. Introduce a flat ESLint config with an import-boundary rule
(`eslint-plugin-boundaries` or `import/no-restricted-paths`) encoding `routes → features (+ own
feature only) → shared → shared` — no feature→feature and no shared→feature imports. Wire a `lint`
script into the web project's `package.json` and into CI.

**Enforced as `error` immediately.** This requires the only current hard boundary violation — the
Session→settings import — to be resolved first (FE-017), and the feature barrels to exist (FE-018).
Note: an import-boundary rule does **not** flag file size, so the pre-existing 300–440-line routes do
not block this rule; their extraction (FE-020/021/022) is independent quality work.

## Acceptance criteria

- [ ] Flat ESLint config with the import-boundary rule at `error` level; `routes → features(+own) →
      shared` encoded.
- [ ] `lint` script added to the web `package.json` and run in CI.
- [ ] `lint` passes cleanly on the current tree (no boundary violations remain).

## Blocked by

- FE-017
- FE-018
