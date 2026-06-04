# 0004 — Create & view a Session (Raw autosave) + per-user privacy

**Phase:** 2 · **Type:** AFK · **Status:** done · **Realizes:** ADR-0003

## What to build

The headline workflow: a user starts a new **Session**, types into it, the **Raw** text autosaves
as a **Draft**, and they can re-open and read it. Enforces the **Privacy invariant** at the data
layer so no user can ever read another user's Session.

- Rich `Session` aggregate (`Create`, draft mutation) in a `Domain/Sessions/` vertical slice —
  `Features/` (MediatR: `CreateSession`, `SaveDraft`, `GetSession`), `Mappings/` (Mapster),
  `Dtos/`, `Services/`, `DomainEvents/`. No lifecycle/status field.
- **EF global query filter** scoping every Session query to the current user's id.
- React: **"start a session" front-and-center** on the home surface; a session editor that autosaves
  the Draft (debounced); a read view.

## Acceptance criteria

- [x] From the home surface a user can start a new Session in one obvious action and begin typing.
- [x] Raw text autosaves (debounced) and survives a page reload of the same Session.
- [x] A second Session can be created on the same day (multiple-per-day).
- [x] User B requesting User A's Session id receives 404/forbidden — never the content (global-filter
      test proving the Privacy invariant).
- [x] Raw text is stored exactly as typed (no server-side mutation).
- [x] Registration-focused integration tests cover create, autosave, get, and cross-user isolation.

## Blocked by

- #0002
