# One JWT, delivered as an HttpOnly cookie (web) or Bearer token (mobile)

## Status

accepted

## Context & decision

There is always a single first-party **JWT** minted by JournalRecall. It is obtained one of two
ways — **(a)** local ASP.NET Core Identity password validation, or **(b)** an external **OIDC**
authorization-code flow — but both paths end identically: *we* mint our own JWT. The token is
delivered to the **web SPA as a strict, HttpOnly, Secure, SameSite cookie** (never readable by
JavaScript) and to **mobile apps as a `Bearer` token** in the `Authorization` header. `JwtBearer`
validates both: `OnMessageReceived` reads the cookie, falling back to the `Authorization` header.

## Considered options

- **Plain ASP.NET Identity cookie (opaque session)** — simplest for web, but gives mobile no
  first-class bearer token and no uniform JWT to validate across clients.
- **JWT in browser JS storage** — standard SPA pattern, but reintroduces token handling and XSS
  exposure the same-origin design (ADR-0001) was chosen to avoid.

## Consequences

- The browser does **zero** token handling; SignalR authenticates over the cookie automatically.
- External OIDC is used for **authentication only** — we federate (verify identity, mint our own
  token) rather than store/refresh upstream tokens. So `Duende.AccessTokenManagement`'s core job
  (refreshing upstream access tokens for downstream calls) has little to do here; expect that
  dependency to end up thinner than the original stack notes implied.
- Mobile and web share one identity/validation path, differing only in token delivery.
