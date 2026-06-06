# TEST-0004 — Functional harness + reference tests

**Phase:** 10 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0003

## What to build

The full-web-host layer: real auth flow, CSRF, the access gate, status codes, JSON shapes, and SSE.
Default is **real auth**; fake auth is opt-in and never routes around middleware. Proven by reference
tests on the Session pilot. Can run in parallel with #TEST-0003.

- **`TestingWebApplicationFactory`** — boots the real host; `CreateAuthenticatedClientAsync()` runs the
  genuine register→login flow and returns an `HttpClient` carrying the real cookie/bearer + `X-CSRF`
  header.
- **`FakeAuthWebApplicationFactory`** — registers a test-only fake authentication scheme **only here**
  (never in `Program`), exposed via `client.AsUser(...)` / `AsAdmin()`. It skips **only** token
  issuance; the request still flows through CSRF and the access gate.
- **`TestBase`**, **`ApiRoutes`** constants, **`HttpClient` JSON helpers** (`HttpClientExtensions`), and
  an **SSE reader helper** (`ReadServerSentEventsAsync()` hiding `text/event-stream` parsing).
- **Reference tests** (`lowercase_with_underscores`): create-session over **real auth**; a fake-auth GET;
  the **SSE `cleanup/stream`** test asserting streamed Cleanup progress end-to-end. Assert HTTP status,
  response shape, headers/cookies, and streamed events.

## Acceptance criteria

- [ ] `CreateAuthenticatedClientAsync()` drives the real register→login flow and the client carries the
      real cookie/bearer + `X-CSRF` header.
- [ ] The fake-auth scheme exists only in `FakeAuthWebApplicationFactory`; `AsUser`/`AsAdmin` authenticate
      a caller while still passing CSRF and the access gate (only token issuance is skipped).
- [ ] `ApiRoutes`, the JSON `HttpClient` helpers, and the SSE reader helper exist and are used by the
      reference tests.
- [ ] Passing functional tests cover: real-auth create-session, a fake-auth GET, and the SSE
      `cleanup/stream` endpoint.
- [ ] Functional tests run in a single serial collection.

## Blocked by

- #TEST-0002
