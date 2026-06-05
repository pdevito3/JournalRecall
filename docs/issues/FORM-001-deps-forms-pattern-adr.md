# FORM-001 — Dependencies + forms-pattern ADR

**Phase:** 11 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0004

## What to build

Lay the groundwork for the shared forms pattern: add the libraries and record the decision so every
later slice has a documented pattern to compose against.

- Add `@tanstack/react-form` (v1.x latest) and `zod` (v4.x latest) to the web client. No
  `@tanstack/zod-form-adapter` — react-form v1 consumes zod schemas directly as Standard Schema
  validators. Must remain compatible with the existing React 19.2 / Vite / TypeScript setup.
- Write a forms-pattern ADR (`docs/adr/00NN-*.md`) recording the decisions already settled in
  PRD-0004: "Level B" abstraction (thin bound field components over react-aria-components, composed
  per form — not raw react-form per form, not a schema-driven auto-renderer); explicit `field` prop
  binding (no `createFormHook`/`fieldContext`); validation timing (zod as form-level `onBlur`,
  revalidate-on-change once errored, submit-gated button); validation-only schema conventions
  (no transforming schemas; comma→array split in submit handlers; export `z.infer` types).
- Cross-link the ADR from the forms area and from PRD-0004.

## Acceptance criteria

- [ ] `@tanstack/react-form` (v1.x) and `zod` (v4.x) are in the web client's `package.json`; install
      succeeds and the client still builds.
- [ ] No `@tanstack/zod-form-adapter` is added.
- [ ] The forms-pattern ADR is committed and records abstraction level, field binding, validation
      timing, and schema conventions per PRD-0004, cross-linked from the relevant area.

## Blocked by

- None - can start immediately
