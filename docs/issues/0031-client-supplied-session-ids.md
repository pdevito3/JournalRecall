# 0031 — Client-supplied Session IDs (idempotent create)

**Phase:** 9 (mobile sync) · **Type:** AFK · **Status:** ready · **Realizes:** ADR-0013 · **Paired with:** journal-recall-ios#0003

## What to build

Let a client mint the Session's GUID so a Session can be created offline and the create call
replayed safely. `POST /sessions` accepts an optional `id`; when present, the server uses it
instead of generating one. Replaying a create with an `id` that already exists **for this user**
is a no-op that returns the existing Session (idempotent), not a duplicate and not an error. An
`id` that exists under another user is rejected without leaking existence (same response shape as
any not-yours resource). Omitting `id` behaves exactly as today — the web client is untouched.

## Acceptance criteria

- [ ] `POST /sessions` with a client `id` creates the Session under that GUID.
- [ ] Replaying the same create (same user, same `id`) returns the existing Session with no
      duplicate row and no error.
- [ ] A colliding `id` owned by a different user is rejected with no existence leak.
- [ ] `POST /sessions` without an `id` still server-mints the GUID (web client unchanged).
- [ ] Integration tests cover all four paths.

## Blocked by

None — can start immediately.
