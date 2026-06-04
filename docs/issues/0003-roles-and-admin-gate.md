# 0003 — Roles & admin gate

**Phase:** 1 · **Type:** AFK · **Status:** todo · **Realizes:** ADR-0002

## What to build

Two roles — **Admin** and **Member** (default) — and a permission-based gate so admin-only actions
are blocked for members. Establishes HeimGuard as the authorization mechanism for the non-journal
admin surface.

- Seed roles; new users are **Member** by default.
- HeimGuard wired with an admin permission; an example admin-only endpoint (e.g. a stub
  `GET /api/admin/ping`) is gated by it.
- `ICurrentUserService` exposes the current user's id and role from the validated principal.

## Acceptance criteria

- [ ] A Member calling the admin-only endpoint receives 403; an Admin receives 200.
- [ ] An unauthenticated caller receives 401.
- [ ] New registrations are assigned the Member role by default.
- [ ] `ICurrentUserService` returns the correct user id and role inside a request.
- [ ] Tests cover member-forbidden, admin-allowed, and anonymous-unauthorized.

## Blocked by

- #0002
