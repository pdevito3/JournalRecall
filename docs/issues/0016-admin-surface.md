# 0016 — Admin surface: user management + AI provider config

**Phase:** 7 · **Type:** AFK · **Status:** done · **Realizes:** ADR-0002

## What to build

The non-journal admin surface: manage users and configure the app-wide AI provider. HeimGuard-gated;
no journal visibility, ever.

- Admin user management: invite/create, disable, and assign Admin/Member roles.
- App-wide **AI provider/model** configuration (BYO OpenAI-compatible endpoint + model) that the
  Cleanup/Summary features consume.
- React admin pages, reachable only by Admins.

## Acceptance criteria

- [ ] An Admin can create/disable users and change a user's role; a Member cannot reach these (403).
- [ ] An Admin can set the OpenAI-compatible endpoint + model; a subsequent Cleanup uses the
      configured provider.
- [ ] The admin surface exposes **no** journal data for any user (Privacy invariant holds — verified
      by absence of any journal-reading admin endpoint).
- [ ] Disabling a user prevents their login.

## Blocked by

- #0003
- #0007
