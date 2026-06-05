# Durable sessions via first-party refresh-token rotation, with hardened cookies

## Status

accepted

## Context & decision

ADR-0002 established a single first-party **JWT**, delivered to the web SPA as an HttpOnly cookie.
That access token is short-lived and, on its own, leaves a User signed out the moment it expires —
contrary to the desired "stay signed in basically forever, like YouTube/Instagram" experience. We
add a **first-party refresh token** alongside it: a short-lived access JWT (~15 min) is silently
renewed by a long-lived, **server-side, revocable** refresh token. Both are minted by JournalRecall
on the **existing `Microsoft.AspNetCore.Identity` + `JwtBearer` packages** — no new dependency.

The refresh token is a 256-bit random value, **stored SHA-256-hashed at rest** (the raw value is
never persisted), and **rotated on every use**: each refresh issues a new token and invalidates the
prior one. Presenting an already-rotated token (**reuse-detection**) revokes the whole chain as
suspected theft; a brief **grace window** plus **client single-flight** keeps double-fired refreshes
from falsely logging anyone out. The refresh window **slides on use with no absolute cap**, so an
active User effectively never re-signs-in, while **logout** (current device), **Admin-disable**, and
**password change** (all-other-devices) remain hard revocation points.

Cookies are hardened: the access cookie is **`__Host-jr_auth`** (`Secure`, `Path=/`, no `Domain`)
and the refresh cookie is **`__Secure-jr_refresh`** (`Secure`, path-scoped to `/api/auth/refresh` —
`__Host-` is unusable there because it forbids a non-`/` path). Both stay `HttpOnly`,
`SameSite=Strict`. Mutating requests additionally require a custom **`X-CSRF`** header as
defense-in-depth. Delivery follows ADR-0002: web gets cookies, mobile gets the same tokens in
response bodies via the same `/api/auth/refresh` endpoint.

## Considered options

- **Long-lived JWT directly in the cookie (no refresh)** — simplest, but a stolen cookie is valid for
  months and **cannot be revoked**, breaking the Admin-disable guarantee. Rejected.
- **Opaque server-side sliding session cookie (classic Identity application cookie)** — easy and
  revocable, but abandons the single-JWT model and gives mobile no clean bearer story. Rejected:
  contradicts ADR-0002.
- **`Duende.AccessTokenManagement`** — manages *upstream* OAuth/OIDC tokens for calling *downstream*
  APIs; here JournalRecall is the token **issuer**, which the library does not address. Rejected as
  the wrong tool — this supersedes ADR-0002's note that it might earn its place.
- **Sliding window with a hard absolute cap** — bounds a silently-stolen token, but forces periodic
  re-login against the "basically never" goal. Rejected for this self-hosted context.

## Consequences

- An actively-used session is effectively **permanent**; the residual risk — a silently stolen token
  whose victim never returns — is **accepted**, bounded only by HttpOnly delivery, rotation +
  reuse-detection, and the disable/logout/password-change kill switches, given the small-trust-circle
  self-hosted framing.
- Adopting the `__Host-`/`__Secure-` prefixes makes **HTTPS required in dev** (cookie names are no
  longer conditioned on scheme); Aspire already serves over TLS, so this is operationally a no-op.
- Refresh rotation requires **server-side token state** (a hashed-token table with rotation linkage)
  and disciplined atomic invalidation; subtle errors here would break detection or create races.
- A **strict Content-Security-Policy** — the actual anti-XSS control — is **deferred** as a
  fast-follow. HttpOnly cookies stop token *theft* via script, but only CSP/safe rendering stop XSS
  from *riding* a live session.
- This refines, and does not replace, ADR-0002: still one first-party JWT validated identically
  across web and mobile — now with a revocable refresh path layered on.
