# FORM-004 — Shared schema fragments (password + email)

**Phase:** 11 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0004

## What to build

The validation rules that are genuinely one decision, extracted as pure-zod fragments so changing the
password policy is one edit that applies everywhere it should.

- A **password fragment**: the password policy plus the password-match `.refine`. This collapses the
  match check currently reimplemented in three places (register, setup, change-password) into one
  fragment.
- An **email fragment** for the auth forms that share it.
- Coupling test: extract only what should change together. Forms that must change in lockstep import
  the fragment; forms that merely look similar do not get coupled through it.
- Pure-zod unit tests on each fragment (policy boundaries, match success/failure, email
  valid/invalid). No rendering — these are validation-only.

## Acceptance criteria

- [ ] A password fragment (policy + match `.refine`) and an email fragment exist as importable pure
      zod, with no React/DOM dependencies.
- [ ] The fragments are validation-only (no transforming schemas).
- [ ] Pure-zod unit tests cover password policy + match and email valid/invalid cases.

## Blocked by

- FORM-001
