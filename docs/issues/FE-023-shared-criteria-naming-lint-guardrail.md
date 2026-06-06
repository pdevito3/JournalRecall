# FE-023 — (NICE) promote-to-shared criterion, naming conventions, derive-don't-sync guardrail

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

Documentation and guardrail polish for the boundary work:

- Document the "promote to `shared`" criterion (used by ≥2 features and domain-agnostic, else promote
  to its own vertical) and codify file-naming conventions (kebab-case files, `useX` hooks, `index`
  barrels) in the lint config / contributing notes.
- Split the large session feature module into types/constants/api behind the barrel.
- Add the lint/review guardrail that flags `useEffect` which `setState`s from query data ("derive or
  `key`, don't sync") so the rule survives future features.

## Acceptance criteria

- [ ] Promote-to-shared criterion + file-naming conventions are documented (lint config or contributing
      notes).
- [ ] The session feature module is split into types/constants/api behind its barrel.
- [ ] A lint rule (or documented review guardrail) flags `useEffect`→`setState`-from-query-data.

## Blocked by

- FE-018
- FE-019
