# PRD 0007 — Session Activity metadata

**Status:** ready-for-agent · **Type:** AFK · **Delivery:** three vertical slices (tracer bullets) —
**A** (capture & persist) → **B** (timeline filter), with **C** (automatic tenant scoping) independent ·
**Introduces:** ADR-0011 (metadata write becomes a full-replace `PUT`, retiring the
nullable-means-don't-touch convention) and ADR-0012 (persistence config lives in per-entity
`IEntityTypeConfiguration<T>` classes; tenant scoping is applied automatically to `ITenantScoped`
entities) · **Touches:**
[ADR-0002](../adr/0002-cookie-wrapped-jwt-auth.md) / [ADR-0005](../adr/0005-refresh-token-rotation-and-cookie-hardening.md)
(the Privacy invariant's enforcement point moves from per-entity hand-wiring to a marker-driven helper;
`RefreshToken` stays deliberately unscoped),
[ADR-0006](../adr/0006-three-layer-test-suite-user-isolated-sqlite.md) (tests).

> Domain language per [`CONTEXT.md`](../../CONTEXT.md), which now defines **Activity**. This PRD adds a
> single new piece of **Metadata** to a **Session** and refines how **Metadata** is written and how
> tenant scoping is configured. The **Privacy invariant** (all journal data strictly per-**User**) and
> the rule that **AI never alters Raw** are preserved throughout. **No production data exists yet** —
> the single initial EF migration is regenerated in the new shape; there is **no backfill**.

## Problem Statement

As a journaling **User**, the **Session** records *how I felt* (**Mood**) and *what it was about*
(**Topic**, **Person**), but nothing about *what I was physically doing while journaling*. The same
five-minute entry means something different when I dictated it on a walk, scribbled it over breakfast,
or sat down deliberately to reflect — and today that context is lost the moment I write. I also can't
ask the journal "what do I tend to write while walking vs. resting?", because there's no such facet to
filter on.

Separately, two seams in the codebase are fragile in ways that bite the moment metadata grows:

- **Metadata writes use a `null`-means-don't-touch contract.** `MetadataForWrite` carries nullable
  `Topics?`/`Moods?` and the handler treats `null` as "leave this field alone." A non-nullable field
  like **Activity** has no honest place in that contract, and the convention already obscures intent
  (is a missing field "clear it" or "ignore it"?).
- **Tenant scoping is hand-wired per entity.** Every tenant-scoped entity repeats a manual
  `HasQueryFilter(...== _currentUserId)` line. The **Privacy invariant** — the single most important
  property of this app — depends on no one ever forgetting that line on a new entity.

## Solution

A **Session** gains exactly one **Activity** — what the **User** was physically doing while
journaling — chosen from a small app-defined set with a free-text escape hatch, captured in the
existing metadata editor and shown on the session card. The journal timeline gains an **Activity**
filter facet. Delivered as three vertical slices:

- **Slice A — Capture & persist an Activity.** Pick an Activity (or leave it `None`); it persists and
  displays. Carries the supporting contract change: metadata writes become a **full-replace `PUT`**.
- **Slice B — Filter the timeline by Activity.** "Show me everything I wrote while walking."
- **Slice C — Automatic tenant scoping.** Tenant scoping is applied automatically to any entity that
  opts in via an `ITenantScoped` marker, so the Privacy invariant can't be silently dropped on a new
  entity. No user-facing change.

**Activity** is a value object: **exactly one per Session** (single-valued, unlike multi-valued
**Mood**/**Topic**), **non-nullable**, defaulting to `None`. A known activity comes from an
app-defined **SmartEnum**; the `Custom` member additionally carries the User's free-text value. It
describes the *act of journaling* (posture/motion), **not** the content — life-areas stay **Topics**,
feelings stay **Moods**. `None` means "didn't say / not applicable" and is the zero value; it is
distinct from `Stationary` ("I was deliberately sitting still"). There is **no `null`/unset state**.

## User Stories

1. As a journaling User, I want to record what I was physically doing while journaling, so that an
   entry's context (walking vs. sitting vs. eating) isn't lost.
2. As a User, I want every Session to start with an Activity of `None`, so that activity is always
   optional and I'm never forced to answer.
3. As a User, I want to pick an Activity from a short, recognizable list (None, Stationary, Walking,
   Eating, Commuting, Exercising, Resting), so that tagging is fast and consistent.
4. As a User, I want to type a custom activity when none of the known ones fit (e.g. "cooking"), so
   that the list never boxes me in.
5. As a User, I want each Session to have exactly one Activity, so that the field stays simple and
   unambiguous (I was doing one thing).
6. As a User, I want `Stationary` to mean "I deliberately sat still" as distinct from `None` ("didn't
   say"), so that an intentional choice reads differently from silence.
7. As a User, I want to change a Session's Activity at any time after creating it, so that I can fix or
   add it after the fact.
8. As a User, I want the Activity to live in the same metadata editor as my Moods and Topics, so that
   all my Session metadata is in one place.
9. As a User, I want the chosen Activity shown on the session card/detail (with a recognizable icon,
   e.g. a couch for Stationary), so that I can see it at a glance.
10. As a User, I want selecting `None` in the picker to represent "no particular activity," so that I
    can leave a Session unmarked or clear a prior choice.
11. As a User, I want to filter my timeline by Activity, so that I can see all the entries I wrote
    while doing a particular thing.
12. As a User, I want the Activity filter to sit alongside the existing Mood/Topic filters, so that I
    can combine facets ("anxious entries written while commuting").
13. As a User, I want my Activity choices to be private to me like all my journal data, so that no one
    else — admins included — can see them.
14. As a User, I never want the AI to guess or overwrite my Activity, so that this field reflects only
    what I said.
15. As a developer, I want a single honest contract for writing Session metadata, so that I don't have
    to reason about whether a missing field means "clear" or "ignore."
16. As a developer, I want the metadata `PUT` to replace the whole metadata object, so that the client
    (which already holds all fields in the editor form) sends a complete, predictable payload.
17. As a developer, I want a new tenant-scoped entity to be protected by the Privacy invariant just by
    implementing a marker interface, so that scoping is consistent and hard to forget.
18. As a developer, I want `RefreshToken` to stay deliberately unscoped, so that token rotation still
    works after the access token has expired (no current user established).
19. As a developer, I want Session's persistence configuration in its own
    `IEntityTypeConfiguration<Session>` class, so that mapping is discoverable and testable in
    isolation.
20. As a developer, I want the Activity value object's canonicalization (known vs. custom, `None`
    default) covered by unit tests, so that the domain rule is pinned independent of storage or UI.
21. As an Admin, I still want zero visibility into any User's Activity (or any journal data), so that
    the Privacy invariant remains absolute.

## Implementation Decisions

### Domain — the `Activity` value object (Slice A)

- **Single-valued, non-nullable, `None`-default.** A Session always has exactly one Activity. There is
  no `null`/unset state; `None` is the zero value and means "didn't say / not applicable."
- **SmartEnum + `Custom` free-text**, modeled on **Mood**. Known set: **None, Stationary, Walking,
  Eating, Commuting, Exercising, Resting**. Any unrecognized value resolves to `Custom` carrying the
  User's words.
- **Sole persisted state is the canonical `string Value`.** "Is this a known member?", the icon key,
  etc. are **derived on demand** by resolving `Value` against the known set — never stored. This shape
  is what lets persistence use `ComplexProperty` with no `ValueConverter` (see below). A `Custom`
  activity persists its **raw free-text** as `Value` (never the literal `"Custom"`), exactly as Mood's
  custom member persists the user's words.
- `Activity.Resolve(string)` performs case-insensitive matching against known members and falls back
  to `Custom(text)`. `None` is the default returned for absent/empty input.
- **`UserSet` only.** Activity carries **no provenance** and is **never** an AI **Suggestion**:
  physical activity is context the text rarely reveals, so the User is the only reliable source.
  Activity is untouched by **Cleanup** and never appears in the `SuggestionKind` flow.
- `Session` gains a non-null `Activity` property (default `None`) and a `SetActivity(Activity)` method.

### Persistence — `ComplexProperty`, not a `ValueConverter` (Slice A)

- Map `Activity` via EF Core 10 **`ComplexProperty`** projecting its single `Value` to an `activity`
  column on the `sessions` table. EF rematerializes the complex type by constructor-binding the column
  back into the value object. No `ValueConverter` is needed precisely because `Value` is the only
  mapped scalar.
- This mapping lives in a new **`SessionConfiguration : IEntityTypeConfiguration<Session>`** — the
  first realization of the per-entity-config convention (ADR-0012). The existing inline Session mapping
  (table, key, ignores, draft backing-field access, Moods JSON collection, indexes, owned collections)
  moves into this class wholesale.
- The single initial EF migration is **regenerated** in the new shape (drop the dev DB first per the
  established dev workflow). No backfill — no production data exists.

### API & contract — full-replace metadata `PUT` (Slice A; ADR-0011)

- `MetadataForWrite` becomes a **complete, non-partial** payload: `Topics` (string list), `Moods`
  (string list), and **`Activity`** (the canonical string). The client (the metadata editor form)
  already holds all fields, so it always sends the whole object.
- `UpdateMetadata` **replaces** all metadata wholesale rather than treating omitted/`null` fields as
  "don't touch." The nullable-means-don't-touch convention is retired.
- The write continues to flow through the existing `PUT /api/sessions/{id}/metadata` endpoint — **no
  new endpoint**. Accepting a **Suggestion** and approving a **Person** proposal keep their own
  endpoints and are unaffected.
- **Deferred:** a future JSON Patch (RFC 6902) contract is noted as a possible later expansion if
  genuine partial-update callers ever emerge; not built now (ADR-0011 records the door left open).
- `SessionDto` gains `Activity` (string); the web `Session`/`Metadata` types gain `activity` (string).

### Timeline filter (Slice B)

- `GET /api/sessions` gains a single-select **Activity** filter facet alongside the existing
  Mood/Topic filters, combinable with them.
- The web timeline gains an Activity filter control.

### Automatic tenant scoping (Slice C; ADR-0012)

- Introduce an **`ITenantScoped`** marker interface (exposes `UserId`). `Session`, `Summary`,
  `Person`, and `Correction` implement it; **`RefreshToken` does not** (it owns a `UserId` but must
  stay unscoped so rotation works with no current user — ADR-0005); the app-wide settings entities have
  no `UserId` and are naturally out.
- A **strongly-typed generic helper** applies the named `TenantFilter` (`e => e.UserId ==
  _currentUserId`) and is invoked over `modelBuilder.Model.GetEntityTypes()` for every `ITenantScoped`
  type, replacing the four hand-written `HasQueryFilter` lines in one atomic change.
- **Privacy footgun, recorded as the rationale:** the filter must reference the DbContext instance
  field `_currentUserId` so EF parameterizes and re-evaluates it **per query**. Capturing the *value*
  at model-build time would bake one User's id into EF's cached compiled model and leak rows across
  tenants. The generic-helper form preserves the exact closure semantics the current code relies on;
  it only selects entities by reflection. This is why the helper is strongly-typed rather than a
  hand-built expression tree.
- Scope: the `IEntityTypeConfiguration<T>` extraction is realized for **`Session`** now (Slice A) and
  the auto-tenant-filter is applied across **all** `ITenantScoped` entities (Slice C); migrating the
  *other* entities' static config into their own configuration classes is **opportunistic**, not
  required by this PRD.

## Testing Decisions

Good tests here assert **external behavior**, not implementation details: the canonical value an
`Activity` resolves to, the rows a query returns, the persisted result of a metadata write — never the
private shape of a converter or the wording of a filter expression. Prior art lives in the three-layer
suite (ADR-0006): pure-domain **unit** tests, in-process **integration/feature** tests over the
User-isolated shared SQLite, and full-HTTP **functional** tests.

- **`Activity` value object (Slice A) — unit.** `Resolve` matches known members case-insensitively;
  unrecognized input becomes `Custom` carrying the raw text; empty/absent input yields `None`; `None`
  is the default/zero value and is distinct from `Stationary`. Models on existing **Mood** value-object
  tests.
- **Full-replace metadata write (Slice A) — functional.** A `PUT /api/sessions/{id}/metadata`
  round-trips `Activity` and replaces Topics/Moods/Activity wholesale; a known activity and a custom
  activity each persist and read back correctly. Models on existing metadata-update tests.
- **Automatic tenant scoping (Slice C) — integration.** An `ITenantScoped` entity created by one User
  is invisible to another (the Privacy invariant holds through the marker-driven helper exactly as it
  did through the hand-wired filters); **`RefreshToken` remains unfiltered** (regression guard for
  ADR-0005). Models on existing tenant-isolation tests.
- Slice **B**'s timeline filter gets a functional test that a filtered timeline returns only Sessions
  with the selected Activity, combinable with a Mood/Topic facet.

## Out of Scope

- **AI suggestion of Activity.** Activity is `UserSet` only; Cleanup never proposes or sets it.
- **Multiple activities per Session** or activity that changes mid-session. Single-valued by decision.
- **Device/sensor auto-capture** (motion sensors, "what were you doing?" prompts). A different
  mechanism entirely, not AI inference; not now.
- **JSON Patch (RFC 6902) metadata contract.** Noted as a deferred possibility in ADR-0011; this PRD
  ships full-replace only.
- **Migrating every entity's static EF config** into its own `IEntityTypeConfiguration<T>` class.
  Realized for `Session` now; the rest is opportunistic.
- **Backfill / data migration.** No production data exists; the initial migration is regenerated.
- **A place label for Location** or any other unrelated metadata change.

## Further Notes

- `CONTEXT.md` has been updated with the **Activity** glossary entry as part of the grilling session
  that produced this PRD.
- Two ADRs accompany this work: **ADR-0011** (full-replace metadata `PUT`, retiring
  nullable-means-don't-touch, JSON-Patch door left open) and **ADR-0012** (per-entity
  `IEntityTypeConfiguration<T>` + marker-driven automatic tenant scoping, with the cached-model
  privacy footgun as the rationale).
- Icons (e.g. a couch for `Stationary`) are a UI detail, intentionally kept out of `CONTEXT.md`.
