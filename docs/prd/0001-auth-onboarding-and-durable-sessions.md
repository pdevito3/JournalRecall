# PRD 0001 — Auth gating, first-run onboarding, and durable sessions

**Status:** ready-for-agent · **Realizes:** ADR-0002 (+ a new ADR-0005 for refresh-token rotation)
· **Type:** AFK · **Delivery:** vertical slices (see *Implementation Decisions → Vertical slices*)

> Domain language per [`CONTEXT.md`](../../CONTEXT.md): **User** (tenant boundary), **Admin**
> (non-journal administration only), **Member** (default role), **Privacy invariant** (no User —
> Admin included — ever reads another User's journal). Auth architecture per
> [ADR-0002](../adr/0002-cookie-wrapped-jwt-auth.md): one first-party JWT, HttpOnly cookie for web /
> Bearer for mobile.

## Problem Statement

As a User of a self-hosted JournalRecall instance, the current auth experience has sharp edges:

- When I'm signed out I can still navigate to journal pages — they render an empty shell instead of
  sending me to sign in. It feels broken and leaks the app's structure.
- There's no guided way to stand up a fresh instance. The first **Admin** has to be granted
  "out-of-band," which is undocumented and error-prone for a home-lab install.
- Registration is wide open. As the operator of a private, self-hosted journal I have no way to close
  my instance so that only people I add can get in.
- When an Admin adds a User, there's no way to hand them a one-time password they're forced to
  replace — the Admin has to know and set the User's permanent password.
- I get silently logged out after an hour (the JWT expires with no refresh) and must sign in again.
  I expect the "stay signed in basically forever" experience of apps like YouTube or Instagram —
  without weakening security.

## Solution

A cohesive onboarding-and-session experience for self-hosted instances:

- **Signed-out users only ever see sign-in / sign-up / setup.** Every other route is gated.
- **First-run setup**: a brand-new instance (zero Users) routes the first visitor to a dedicated
  setup page that creates the **root Admin**.
- **Operator-controlled registration**: after setup, an Admin can allow self-registration or close
  the instance so Users are added only by an Admin. Closed is the default.
- **Temporary passwords**: an Admin can create (or reset) a User with a temporary password the User
  must change on first sign-in.
- **Durable sessions**: an active User effectively never has to sign in again, backed by securely
  rotated refresh tokens that remain fully revocable (logout, Admin-disable, password change).

No email/SMTP is introduced — the entire feature set is mail-free (no email verification, no
emailed reset links; recovery is Admin-driven via the temporary-password flow).

## User Stories

### Access gating (signed-out experience)

1. As a signed-out visitor, I want any protected `/app/*` route to redirect me to sign in, so that I
   never land on a broken empty page.
2. As a signed-out visitor, I want to still reach the sign-in, sign-up, and setup pages, so that I
   can actually get into the app.
3. As a signed-out visitor on a fresh instance, I want to be routed to the setup page instead of
   sign-in, so that I'm guided to create the first account.
4. As a signed-out visitor who deep-links to a protected page, I want the server itself to redirect
   me before the app even loads, so that I never receive the page for content I can't see.
5. As a signed-in User who navigates within the app, I want the client to also guard routes, so that
   redirects are instant and don't require a round-trip.

### First-run setup & root Admin

6. As the operator of a brand-new instance, I want a dedicated setup page when no Users exist, so
   that I can create the first **Admin** without manual database work.
7. As the operator, I want the first account I create to automatically be an **Admin**, so that the
   instance has an administrator from the very first User.
8. As the operator, I want to type my own password during setup, so that I'm not handed a generated
   one.
9. As the operator, I want the setup endpoint to refuse to run once any User exists, so that nobody
   can hijack first-run setup later.
10. As the operator, I want concurrent setup attempts to resolve to exactly one root Admin, so that a
    race can't create two "first" Admins.

### Registration control

11. As an **Admin**, I want self-registration to be **off by default** on a new instance, so that my
    private journal is closed unless I deliberately open it.
12. As an **Admin**, I want to toggle self-registration on or off, so that I can decide whether people
    can sign themselves up.
13. As a signed-out visitor, I want the "Create an account" option to appear only when
    self-registration is enabled, so that I'm not offered something that won't work.
14. As a signed-out visitor, I want a deep-link to the register page to redirect me to sign-in when
    registration is disabled, so that the closed state is enforced everywhere.
15. As an **Admin**, I want the register API to reject sign-ups when registration is off, so that the
    UI being convenient doesn't become a security hole.
16. As a self-registering User, I want to be assigned the **Member** role by default, so that I get my
    own private journal and no admin surface.

### Admin-managed users & temporary passwords

17. As an **Admin**, I want to create a User with a temporary password, so that I can onboard someone
    without knowing their permanent password.
18. As an **Admin**, I want to type the temporary password and share it out-of-band, so that no email
    stack is required.
19. As a User given a temporary password, I want to be forced to set my own password on first
    sign-in, so that the Admin never knows my real password.
20. As a User in the forced-change state, I want every other page and API blocked until I change my
    password, so that I can't slip past the requirement.
21. As a User, I want setting my new password to clear the requirement and drop me into the app, so
    that the interruption happens exactly once.
22. As an **Admin**, I want to reset a User who forgot their password by issuing a new temporary
    password, so that recovery works without email.
23. As an **Admin**, I want to assign a created User the **Admin** or **Member** role, so that I can
    delegate administration.

### Durable, secure sessions

24. As a signed-in User, I want to stay signed in indefinitely as long as I keep using the app, so
    that I'm not logged out like I am today after an hour.
25. As a signed-in User, I want my session to refresh silently in the background, so that I never see
    an interruption.
26. As a User, I want signing out to end the session on *this* device only, so that logging out my
    laptop doesn't sign out my phone.
27. As an **Admin**, I want disabling a User to revoke *all* their sessions, so that a disabled User
    is locked out everywhere, not just at next sign-in.
28. As a User, I want changing my password to revoke my other sessions, so that a password change is
    an effective security action.
29. As a security-conscious operator, I want refresh tokens stored hashed and rotated on every use,
    so that a stolen or leaked token is detectable and short-lived in practice.
30. As a security-conscious operator, I want a reused (already-rotated) refresh token to revoke the
    whole token chain, so that token theft is caught when the legitimate User returns.
31. As an active User on a flaky connection, I want a brief grace window / single-flight refresh, so
    that a double-fired refresh doesn't falsely log me out.
32. As a disabled User holding a still-valid access token, I want to be locked out within minutes
    (short access-token lifetime), so that disabling takes effect promptly.

### Hardening

33. As an operator, I want auth cookies to use the `__Host-`/`__Secure-` prefixes, so that subdomain
    cookie-injection and fixation are prevented.
34. As an operator, I want mutating requests to require a custom `X-CSRF` header on top of
    `SameSite=Strict`, so that cross-site request forgery has defense-in-depth.
35. As an operator, I want the **Privacy invariant** to remain absolute throughout, so that none of
    these admin/onboarding surfaces ever expose another User's journal.

## Implementation Decisions

### Architecture & ADRs

- Stays within **ADR-0002**: still a single first-party JWT, delivered as an HttpOnly cookie (web) or
  Bearer token (mobile). Refresh-token rotation is **new first-party code on the existing
  `Microsoft.AspNetCore.Identity` + `JwtBearer` packages** — no new heavy dependency.
- **`Duende.AccessTokenManagement` is explicitly NOT used.** It manages *upstream* OAuth/OIDC tokens
  for calling downstream APIs; here JournalRecall is the *issuer* of its own tokens, which the
  library does not address. A new **ADR-0005** records the refresh-token rotation + cookie-hardening
  model; ADR-0002 gets a one-line amendment pointing to it and confirming Duende is out for this work.

### Modules (deep modules favored for isolated testing)

- **`RefreshTokenService`** *(deep module)* — the encapsulated refresh lifecycle: `Issue`, `Rotate`,
  `RevokeCurrent`, `RevokeAll`. Internals: high-entropy (256-bit) random token, **SHA-256 hashed at
  rest** (raw token never persisted), rotation linkage, **reuse-detection** (presenting an
  already-rotated token revokes the whole chain), a short **grace window** for just-rotated tokens,
  and a **sliding expiry with no absolute cap**. Simple interface, lots of logic, rarely changes.
- **`RefreshToken`** entity + EF mapping: hashed token, `UserId`, `ExpiresAt`, rotation/replaced-by
  linkage, optional device/user-agent *label* (for a future "your sessions" view). **Not** bound to
  IP (avoids false logouts on network changes).
- **`AuthSettings`** singleton entity + reader/writer, mirroring `AiProviderSettings` (one row per
  installation, Admin-only). Holds `SelfRegistrationEnabled` (**default false**). Lazy-created.
- **Setup module**: `POST /api/setup` creates the first User as **Admin** guarded by an **atomic
  zero-users re-check** (transaction / unique constraint); returns **409 Conflict** once any User
  exists. Bypasses `SelfRegistrationEnabled` (bootstrap is not registration). Root Admin types their
  own password (no temp-password flag).
- **Public config endpoint**: unauthenticated `GET /api/auth/config` → `{ needsSetup,
  selfRegistrationEnabled }`. Drives all anonymous routing (server gate + client guard). `needsSetup`
  is computed as "zero Users exist."
- **Access-gate middleware** (server): validates the access JWT, holds the public-route allowlist
  (`login`, `register` *only when enabled*, `setup`), and 302s anonymous `/app/*` to `/setup` when
  `needsSetup` else `/login`. Client-side TanStack `beforeLoad` guard mirrors it for in-app nav.
- **CSRF header check** (server middleware): mutating requests must carry `X-CSRF` (a custom header
  browsers cannot set cross-origin without a CORS preflight we don't approve). Layered on
  `SameSite=Strict`.
- **Forced-change flow**: new `User.MustChangePassword` bool. Admin-created Users / Admin resets set a
  temp password + flag. On login the User receives a normal session, but a server **sentinel
  (`403 password_change_required`)** rejects all calls except the small allowlist (the new
  change-own-password endpoint, refresh, logout, `/me`, and `/api/auth/config`); the SPA confines the
  User to a "set new password" screen. Setting the new password clears the flag.
- **`AuthCookie` extension**: access cookie `__Host-jr_auth` (`Secure`, `Path=/`, no `Domain`);
  refresh cookie `__Secure-jr_refresh` (`Secure`, **path-scoped to `/api/auth/refresh`** — `__Host-`
  can't be used because it forbids a non-`/` path). Both `HttpOnly`, `SameSite=Strict`. Requires
  **HTTPS in dev** (cookie names are no longer conditional on scheme).
- **Password policy** (static config): `RequiredLength = 10`; `RequireDigit`, `RequireLowercase`,
  `RequireUppercase`, `RequireNonAlphanumeric` all explicitly **false**. (Identity defaults these four
  to *true*, so they must be turned off explicitly. NIST-aligned: favor length over composition.) No
  admin UI for password rules — req for admin-configurable rules is descoped.

### API contracts (shapes, not paths-as-truth)

- `GET /api/auth/config` → `{ needsSetup: bool, selfRegistrationEnabled: bool }` (anonymous).
- `POST /api/setup` (anonymous, first-run only) → creates root **Admin**; **409** if any User exists.
- `POST /api/auth/refresh` → rotates the refresh token, mints a new access JWT, sets both cookies
  (web) / returns the token body (mobile).
- `POST /api/auth/register` → enforces `SelfRegistrationEnabled` server-side; **403** when off;
  assigns **Member**.
- `POST /api/auth/logout` → revokes the current device's refresh token + clears both cookies.
- Change-own-password endpoint → clears `MustChangePassword`, revokes the User's *other* sessions.
- Admin create/reset User → may set a temporary password + `MustChangePassword`; Admin-disable
  revokes *all* the User's refresh tokens.

### Session lifetime decisions

- **Access JWT ~15 minutes** (revocation latency; silent refresh hides it).
- **Refresh token: sliding window, no absolute cap** (e.g. 60-day inactivity window that resets on
  every use), rotation + reuse-detection, grace window / **client single-flight** on refresh.
- **Logout = current device**; **Admin-disable / password-change = revoke all** of a User's sessions.

### Frontend

- TanStack route guards + a public `auth/config` fetch; new `/setup` page; forced-change screen; a
  **single-flight 401→`/api/auth/refresh`→retry** interceptor; conditional register route/link;
  Admin surface additions (create-with-temp-password, the registration toggle, password reset).

### Vertical slices (independently shippable; final cut handled by `to-issues`)

Each slice is an end-to-end tracer bullet (API + tests + minimal UI), grabbable on its own:

1. **Durable sessions**: `RefreshTokenService` + `RefreshToken` table + `/api/auth/refresh` +
   short-lived access token + cookie hardening (`__Host-`/`__Secure-`, `X-CSRF`) + client
   single-flight refresh interceptor. (Foundational; delivers req #7 + hardening.)
2. **Access gate**: server middleware + client guard + `GET /api/auth/config`. (req #1.)
3. **First-run setup**: `/api/setup` atomic root-Admin creation + `/setup` page. (req #2.)
4. **Registration control**: `AuthSettings` singleton + toggle + register enforcement + conditional
   UI. (reqs #3, #4.)
5. **Temp passwords & forced change**: `MustChangePassword` + change-own-password + admin
   create/reset with temp password + sentinel enforcement. (reqs #5, #12 recovery.)

Suggested build order: **1 → 3 → 4 → 5**, with **2** slotting in after **3** (so the gate already
knows about `needsSetup`). Password-policy config change rides along with slice 3 (setup needs it).

## Testing Decisions

A good test here asserts **external behavior** — HTTP status, redirect target, `Set-Cookie`
attributes, whether a session survives — never internal call sequences. Prior art:
`tests/JournalRecall.Api.Tests/AuthTests.cs`, `AdminGateTests.cs`, `AdminSurfaceTests.cs` (endpoint
integration via `WebApplicationFactory` + Shouldly/xUnit), and isolated domain unit tests under
`tests/JournalRecall.Api.Tests/Domain/` (e.g. `MoodTests`, `SessionAggregateTests`).

Modules to test (high-signal, requirement-confirming):

- **`RefreshTokenService`** *(isolated unit tests, domain-style)* — issue→rotate produces a new token
  and invalidates the prior; presenting a rotated token (reuse) revokes the chain; the grace window
  permits a just-rotated token briefly; the sliding window extends on use and never hard-caps; tokens
  are stored hashed (raw value never retrievable). The deepest logic → the densest unit coverage.
- **Durable session, end-to-end** *(integration)* — after access-token expiry a `/api/auth/refresh`
  re-establishes access and rotates cookies; logout revokes only the current device; Admin-disable
  and password-change revoke all sessions; cookies carry `__Host-`/`__Secure-` prefixes, `HttpOnly`,
  `SameSite=Strict`; a mutating request without `X-CSRF` is rejected.
- **Setup + root Admin** *(integration)* — first `POST /api/setup` creates an **Admin**; a second
  returns **409**; concurrent attempts yield exactly one root Admin; `GET /api/auth/config` reflects
  `needsSetup` flipping false afterward.
- **Access gate + CSRF** *(integration)* — anonymous `/app/*` redirects to `/setup` when no Users
  else `/login`; allowlisted auth routes pass; `register` is allowlisted only when enabled; `X-CSRF`
  enforced on mutations.
- **Forced-change + registration toggle** *(integration)* — `MustChangePassword` sentinel blocks all
  non-allowlisted calls until the password is changed, then clears; Admin temp-password reset puts a
  User back into the forced-change state; `POST /api/auth/register` returns **403** when
  self-registration is off and assigns **Member** when on.
- **Privacy invariant guard** *(integration)* — none of the new admin/setup/onboarding endpoints
  expose any User's journal data (verified by absence + a negative test), preserving the invariant
  already asserted around the admin surface.

## Out of Scope

- **Admin-configurable password rules** (req #6) — descoped to a static length-10/no-composition
  policy.
- **Email / SMTP anything** — no email verification, no emailed reset links. Recovery is Admin-driven
  via temporary passwords.
- **Strict Content-Security-Policy** — deferred as a fast-follow (it is the real anti-XSS control, but
  tuning it for the SPA is separable from this work).
- **User-facing "sign out everywhere" / a sessions-list UI** — the data model leaves room (device
  label), but the surface is a later add; for now logout is current-device and Admin-disable is the
  global kill switch.
- **DPoP / sender-constrained tokens, PKCE** — PKCE applies only to external-OIDC authorization-code
  flows (not the first-party password login); DPoP is overkill for the cookie web flow. Both may be
  revisited for mobile / external OIDC later.
- **External OIDC providers** — unchanged from the existing deferred status.

## Further Notes

- "No absolute cap" on the refresh window is a deliberate, self-hosted-appropriate trade: an actively
  used session is effectively permanent, while theft is bounded by HttpOnly delivery, rotation +
  reuse-detection, and the Admin/disable/password-change kill switches. The one residual risk it
  cannot bound — a silently stolen token whose victim never returns — was accepted given the
  small-trust-circle, self-hosted framing.
- Adopting the cookie prefixes makes **HTTPS required in dev**; Aspire already exposes the app over
  TLS, so this should be a no-op operationally — but it must be verified during slice 1.
- The change-own-password endpoint is net-new (no existing change/reset endpoint in the codebase) and
  is reused by both the forced-change flow and Admin-driven reset.
