# 0026 — `ValueObject` base class + `Username` value object

**Phase:** 9 · **Type:** AFK · **Status:** ready

## What to build

The domain foundation for switching identity from email to username (see #0027). Two domain pieces, no
surface change yet — provable on its own via unit tests.

- A reusable reflection-based **`ValueObject`** base class (the jhewlett `ValueObject` pattern:
  structural equality over public properties/fields, `==`/`!=` operators, `GetHashCode`, and an
  `IgnoreMember` opt-out attribute), namespaced for JournalRecall's domain. This is a new shared
  primitive; existing value objects (`Mood`, `Location`) are **not** retrofitted in this slice.
- A **`Username`** value object extending `ValueObject`, exposing a `Value` string and a **throwing
  `Create(string)`** factory that is the single source of truth for username **format + length**:
  - trims input,
  - allowed characters `[a-zA-Z0-9._-]`,
  - length **min 3 / max 32**,
  - on violation throws `ValidationException("username", …)` so the existing Hellang ProblemDetails
    pipeline maps it to **422** and the frontend `applyServerErrors` surfaces it inline on the field
    (matching the established `Mood`/`Location` throwing-factory pattern).
  - **Uniqueness is explicitly out of scope** — that stays with ASP.NET Identity (a store lookup), not
    the value object.

## Acceptance criteria

- [ ] `ValueObject` base class exists with structural equality (`Equals`, `==`/`!=`, `GetHashCode`) and
      an `IgnoreMember` attribute; two `Username`s with the same value compare equal.
- [ ] `Username.Create` trims, enforces charset `[a-zA-Z0-9._-]` and length 3–32, and returns a
      `Username` whose `Value` is the trimmed input.
- [ ] Invalid input (too short, too long, illegal char, null/whitespace) throws
      `ValidationException` keyed on `"username"`.
- [ ] Unit tests cover valid creation, trimming, each rejection case, and value equality.
- [ ] No production construction sites are changed yet (that is #0027); existing `Mood`/`Location`
      value objects are untouched.

## Blocked by

- None - can start immediately
