# 0021 — First-run setup & root Admin

**Phase:** 9 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0001

## What to build

A brand-new instance (zero Users) guides the operator through creating the first account, which is
automatically an **Admin**. End-to-end: a fresh install routes to a `/setup` page where the operator
types their own password; the resulting account is the root Admin; the setup endpoint refuses to run
once any User exists.

- **`POST /api/setup`** (anonymous, first-run only) creates the first User as **Admin**, guarded by
  an **atomic zero-users re-check** (transaction / unique constraint) so concurrent attempts resolve
  to exactly one root Admin. Returns **409 Conflict** once any User exists. Bypasses
  `SelfRegistrationEnabled` (bootstrap is not registration). The operator types their own password —
  no temp-password flag.
- **`/setup` page** (React) collecting the root Admin's credentials.
- **Password policy** (static config): `RequiredLength = 10`; `RequireDigit`, `RequireLowercase`,
  `RequireUppercase`, `RequireNonAlphanumeric` all explicitly **false** (Identity defaults these to
  true, so they must be turned off). NIST-aligned: length over composition.

## Acceptance criteria

- [ ] The first `POST /api/setup` creates a User with the **Admin** role using the operator-supplied
      password.
- [ ] A second `POST /api/setup` returns **409**; concurrent attempts yield exactly one root Admin.
- [ ] The password policy enforces length 10 with no composition requirements.
- [ ] Integration tests cover first-setup-creates-Admin, second-setup-409, and the concurrent-attempt
      single-Admin outcome.

## Blocked by

- #0019
