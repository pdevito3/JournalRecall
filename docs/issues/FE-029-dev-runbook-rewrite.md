# FE-029 — Rewrite the dev-runbook e2e guidance

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

Rewrite the dev-runbook's dev-browser / e2e section to teach `getByRole` / `getByLabel` /
`getByRole('alert')` locators and web-first auto-retrying assertions, and to **demote CSS selectors**
(`input[name=…]`, `span.text-red-400`, `p[role="alert"]`) and `sleep`/`networkidle` waits to a
last-resort fallback — the current guidance teaches exactly the anti-patterns Playwright warns
against, even though the accessible react-aria UI makes role/label locators free.

Point all examples at the committed helper module (FE-028) so login/setup logic is written once. Cover
`exact` label matching on multi-password forms, scoping locators to a region, and stubbing/skipping
geolocation (**Location**) and AI-provider surfaces.

## Acceptance criteria

- [ ] The dev-runbook teaches role/label locators + web-first assertions; CSS selectors and
      `sleep`/`networkidle` are demoted to a documented last-resort fallback.
- [ ] Examples consume the FE-028 helper module rather than re-typing login/setup.
- [ ] Guidance covers `exact` label matching, region scoping, and stub/skip of geolocation + AI-provider
      surfaces.

## Blocked by

- FE-028
