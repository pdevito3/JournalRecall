# FORM-002 — `ProblemError` + ProblemDetails parser (API-client seam)

**Phase:** 11 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0004

## What to build

Stop the API client from destroying server-side field errors. Today the `problem()` helper eagerly
flattens an ASP.NET `ValidationProblemDetails` into a single joined `Error.message`, so the per-field
`errors` dict is gone before any form's `onError` runs. Replace that with a structured seam.

- A ProblemDetails parser that takes an HTTP `Response` (and a fallback message) and returns/throws a
  `ProblemError` whose `.problem` is the parsed ProblemDetails object (`type`/`title`/`detail`/
  `status` + `errors` dict) and whose `.message` is the existing flattened fallback string — so any
  existing code reading `error.message` keeps working unchanged.
- Replace the per-module `problem()` flattening in the API client layer (auth + admin clients) with
  this parser. Designed against the current ASP.NET `ValidationProblemDetails` shape and
  forward-compatible with the upcoming server-side ProblemDetails standardization (out of scope here —
  this only consumes the already-ProblemDetails-shaped responses).
- Unit tests against representative responses: a `ValidationProblemDetails` with an `errors` dict; a
  problem with only `title`/`detail`; a non-problem / opaque body. Assert both the parsed `.problem`
  and the flattened `.message` fallback.

## Acceptance criteria

- [ ] The API client throws a `ProblemError` carrying the parsed `.problem` object; `.message` retains
      the flattened fallback string.
- [ ] The eager `problem()` flattening is removed from the auth and admin API clients; existing
      `error.message` readers still work.
- [ ] Unit tests cover the three response shapes (validation errors dict, title/detail-only,
      non-problem body) and assert `.problem` and `.message`.

## Blocked by

- None - can start immediately
