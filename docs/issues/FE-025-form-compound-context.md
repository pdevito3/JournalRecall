# FE-025 — Form compound context + throwing `useFormContext()` + `Form.Submit`/`Form.Errors`

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005 / ADR-0008

## What to build

Introduce a type-safe form **context** (`createContext` defaulting to a missing sentinel) and a
`useFormContext()` hook that throws a clear, developer-readable error when a `Form.*` sub-component is
used outside `<Form>`. The `FormShell` chrome becomes the `<Form>` compound parent that provides the
form; `Form.Submit` and `Form.Errors` read the form from context and are attached as static
sub-components, centralizing the duplicated error-message rendering. Sub-components read the form from
context instead of every call site prop-drilling it.

## Acceptance criteria

- [ ] A `<Form>` compound parent provides the form via a type-safe context; `Form.Submit` and
      `Form.Errors` are static sub-components reading context.
- [ ] `useFormContext()` throws a clear error outside `<Form>` and returns the form within it.
- [ ] A unit test asserts the throw-outside / return-within behavior of `useFormContext()`.

## Blocked by

- FE-024
