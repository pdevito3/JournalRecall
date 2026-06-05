# FORM-003 — `applyServerErrors(form, error)` helper

**Phase:** 11 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0004

## What to build

One shared helper that maps a caught server error onto a react-form instance, written once and wired
identically into every form's `onError`/`onSubmitInvalid`.

- `applyServerErrors(form, error)`: given a react-form instance and a caught error — if it is a
  `ProblemError` with an `errors` dict, map each entry's key (server casing → field name) onto the
  matching form field's errors so the message renders under that field. Route unmatched keys, a bare
  `title`/`detail`, or any non-`ProblemError` to a single form-level error banner.
- Unit tests: given a `ProblemError` and a form, assert field-keyed errors land on the matching
  fields; assert unmatched keys / bare title / a non-`ProblemError` land on the form-level banner; and
  assert server key casing maps correctly to field names. Tests verify external behavior (the
  error/value a consumer observes), never react-form internal state shape.

## Acceptance criteria

- [ ] `applyServerErrors(form, error)` exists and maps `ProblemError.errors` entries onto matching
      form fields by name (handling server→field casing).
- [ ] Unmatched keys, a bare `title`/`detail`, and non-`ProblemError` errors all route to a single
      form-level banner.
- [ ] Unit tests cover field-keyed mapping, banner fallback, and casing, asserting observable
      behavior rather than internal state.

## Blocked by

- FORM-001
- FORM-002
