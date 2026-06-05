# 0022 — Access gate (server + client) & public auth config

**Phase:** 9 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0001

## What to build

Signed-out visitors only ever see sign-in / sign-up / setup; every other route is gated, enforced
both at the server (before the app loads) and in the client (instant in-app redirects). End-to-end: a
deep-link to a protected page as an anonymous user 302s to `/setup` on a fresh instance else
`/login`, while allowlisted auth routes pass through.

- **Access-gate middleware** (server): validates the access JWT, holds the public-route allowlist
  (`login`, `register` *only when enabled*, `setup`), and **302**s anonymous `/app/*` to `/setup`
  when `needsSetup` else `/login`.
- **Client guard** (TanStack `beforeLoad`): mirrors the server gate for in-app navigation so
  redirects are instant and don't require a round-trip.
- **`GET /api/auth/config`** (anonymous) → `{ needsSetup, selfRegistrationEnabled }`, where
  `needsSetup` is computed as "zero Users exist." Drives all anonymous routing.

`selfRegistrationEnabled` is surfaced by this endpoint but its toggle/enforcement lands in #0023; here
it can be a fixed value until `AuthSettings` exists.

## Acceptance criteria

- [ ] An anonymous request to a protected `/app/*` route 302s to `/setup` when no Users exist, else
      to `/login`.
- [ ] Allowlisted auth routes (`login`, `setup`) pass through for anonymous users.
- [ ] The client `beforeLoad` guard redirects in-app navigation without a server round-trip.
- [ ] `GET /api/auth/config` is reachable anonymously and reports `needsSetup` flipping to false once
      a User exists.
- [ ] Integration tests cover the anonymous redirect targets, allowlist pass-through, and the config
      shape.

## Blocked by

- #0021
