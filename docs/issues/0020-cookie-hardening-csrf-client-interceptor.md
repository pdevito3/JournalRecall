# 0020 — Cookie hardening (`__Host-`/`__Secure-`, `X-CSRF`) + client single-flight refresh

**Phase:** 9 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0001, ADR-0005

## What to build

Harden how the auth cookies are delivered and make refresh invisible to the User. End-to-end: a
browser session uses prefix-hardened cookies, mutating requests are CSRF-protected, and a 401 mid-use
silently refreshes and retries the original request without the User noticing.

- **`AuthCookie` hardening**: access cookie `__Host-jr_auth` (`Secure`, `Path=/`, no `Domain`);
  refresh cookie `__Secure-jr_refresh` (`Secure`, **path-scoped to `/api/auth/refresh`** — `__Host-`
  is unusable there because it forbids a non-`/` path). Both `HttpOnly`, `SameSite=Strict`. Cookie
  names are no longer conditional on scheme, so **HTTPS is required in dev** (Aspire already serves
  over TLS — verify this is a no-op operationally).
- **CSRF middleware**: mutating requests must carry a custom **`X-CSRF`** header (a header browsers
  cannot set cross-origin without a CORS preflight we don't approve), layered on `SameSite=Strict`.
- **Client single-flight refresh interceptor**: a `401 → POST /api/auth/refresh → retry` interceptor
  in the SPA that coalesces concurrent 401s into a **single** refresh call so a double-fired refresh
  never falsely logs the User out.

## Acceptance criteria

- [ ] The auth cookies carry the `__Host-`/`__Secure-` prefixes, `HttpOnly`, and `SameSite=Strict`;
      the refresh cookie is path-scoped to `/api/auth/refresh`.
- [ ] The app boots and authenticates over HTTPS in dev (Aspire); cookie names are unconditional.
- [ ] A mutating request without `X-CSRF` is rejected; the same request with the header succeeds.
- [ ] In the SPA, a 401 transparently triggers one refresh and the original request is retried;
      concurrent 401s share a single in-flight refresh (no false logout).
- [ ] Integration tests assert the `Set-Cookie` attributes and the `X-CSRF` enforcement.

## Blocked by

- #0019
