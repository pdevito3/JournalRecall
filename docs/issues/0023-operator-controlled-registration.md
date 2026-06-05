# 0023 — Operator-controlled registration

**Phase:** 9 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0001

## What to build

An operator can open or close their instance to self-registration, **closed by default**. End-to-end:
when registration is off the "Create an account" option is hidden, deep-links to register redirect to
sign-in, and the register API rejects sign-ups; when on, a self-registering User is created as a
**Member**.

- **`AuthSettings`** singleton entity + reader/writer, mirroring `AiProviderSettings` (one row per
  installation, Admin-only, lazy-created). Holds `SelfRegistrationEnabled` (**default false**).
- **Admin toggle** for `SelfRegistrationEnabled`.
- **`GET /api/auth/config`** now reports the real `selfRegistrationEnabled` from `AuthSettings`.
- **`POST /api/auth/register`** enforces `SelfRegistrationEnabled` server-side: **403** when off;
  assigns the **Member** role when on.
- **Conditional UI**: the register route/link appears only when enabled; the gate's allowlist admits
  `register` only when enabled (a deep-link otherwise redirects to sign-in).

## Acceptance criteria

- [ ] A new instance has `SelfRegistrationEnabled = false` by default; an Admin can toggle it on/off.
- [ ] `POST /api/auth/register` returns **403** when registration is off and creates a **Member** when
      on.
- [ ] The register route/link is shown only when enabled; an anonymous deep-link to register
      redirects to sign-in when disabled.
- [ ] `GET /api/auth/config` reflects the current `selfRegistrationEnabled`.
- [ ] Integration tests cover the default-closed state, the 403-when-off / Member-when-on register
      behavior, and the conditional allowlisting.

## Blocked by

- #0022
