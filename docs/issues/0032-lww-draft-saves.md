# 0032 — Draft saves carry base revision + client timestamp (LWW ordering)

**Phase:** 9 (mobile sync) · **Type:** AFK · **Status:** ready · **Realizes:** ADR-0013 · **Paired with:** journal-recall-ios#0005

## What to build

Make `PUT /sessions/{id}/draft` safe for offline replay. The request gains optional
`baseRevisionNumber` (the Raw Revision the client's edit was based on) and `clientSavedAt` (when
the user actually saved). Ordering is **last-write-wins by save time, with every contender kept as
a Revision**: if the incoming save is based on the current head, it appends and becomes current as
today; if the head moved in the meantime (a concurrent web edit), the incoming content still
appends to the Revision stream, and whichever contender has the **later save time** is the current
Draft. Nothing is ever discarded. Requests without the new fields (the web client) behave exactly
as today.

The same optional `clientSavedAt` skip-if-older rule applies to the other user-owned full-replace
writes (metadata, cleaned hand-edits, corrections, settings) so queued offline edits can't clobber
newer server state.

## Acceptance criteria

- [ ] A replayed offline save based on the current head appends a Revision and becomes current
      (today's behavior, plus the new fields accepted).
- [ ] When the server head changed after the client's base revision: both contenders exist as
      Revisions, and the later `clientSavedAt` one is the current Draft — verified in both arrival
      orders.
- [ ] Plaintext projection and Stale derivation reflect whichever contender won.
- [ ] A full-replace write (e.g. metadata) with `clientSavedAt` older than the entity's last write
      is skipped, not applied.
- [ ] Requests without the new fields behave byte-for-byte as before (web client unchanged).

## Blocked by

None — can start immediately.
