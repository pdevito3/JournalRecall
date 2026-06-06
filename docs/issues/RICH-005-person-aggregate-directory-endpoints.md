# RICH-005 — `Person` aggregate + directory endpoints

**Phase:** 13 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0006

## What to build

Promote **Person** from an owned name-string on **Session** to a first-class per-**User** aggregate
with its own directory, so the people a **User** references are durable entities rather than loose
repeated strings. This is the data foundation for `@`-mentions (RICH-006/007).

End-to-end behavior:

- **`Person` aggregate** (new EF entity, per-**User**): `Id`, `UserId`, `Label`, with schema room for
  aliases later (aliases themselves are out of scope). Tenanted — one **User**'s directory is never
  visible to another (Privacy invariant).
- **`SessionPerson` changes role** from owning a name string to holding a `PersonId` reference. People
  **provenance is dropped** (it moves to the proposal flow in RICH-009).
- **Endpoints:** `GET /people` (per-User directory; powers autocomplete + resolution),
  `POST /people` (create), `PATCH /people/{id}` (rename — propagates because mentions reference
  `PersonId`, not the label).
- **Indices:** `Person(UserId, Label)`; unique `SessionPerson(SessionId, PersonId)`;
  `SessionPerson(PersonId)` for reverse lookup (the reverse index for a future Person filter; the
  filter UI itself is out of scope).
- Adds an incremental migration onto the RICH-003 baseline.

## Acceptance criteria

- [ ] A `Person` aggregate exists (Id, UserId, Label, alias-ready) and is independently queryable
      per-User; one User's People are not visible to another.
- [ ] `SessionPerson` holds a `PersonId` reference (no name string, no People provenance).
- [ ] `GET /people`, `POST /people`, `PATCH /people/{id}` work end-to-end; renaming a Person via PATCH
      updates the directory entry the references point at.
- [ ] Indices exist: `Person(UserId, Label)`, unique `SessionPerson(SessionId, PersonId)`,
      `SessionPerson(PersonId)`.
- [ ] Ships with unit + integration coverage (endpoint request→response + persisted state, per-User
      isolation).

## Blocked by

- RICH-003
