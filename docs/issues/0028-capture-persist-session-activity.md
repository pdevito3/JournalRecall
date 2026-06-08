# 0028 — Capture & persist a Session Activity

**Type:** AFK · **Status:** ready-for-agent · **Realizes:** [PRD-0007](../prd/0007-session-activity-metadata.md), [ADR-0011](../adr/0011-full-replace-metadata-write.md) · **Touches:** [ADR-0012](../adr/0012-per-entity-config-and-automatic-tenant-scoping.md) (introduces the first `IEntityTypeConfiguration<T>`)

## Parent

[PRD-0007 — Session Activity metadata](../prd/0007-session-activity-metadata.md)

## What to build

Give every **Session** exactly one **Activity** — what the **User** was physically doing while
journaling — captured in the existing metadata editor and shown on the session card. This is the
end-to-end tracer bullet: pick an Activity (or leave it `None`), it persists, it displays.

- **`Activity` value object.** Single-valued, **non-nullable**, defaulting to `None`. SmartEnum +
  `Custom` free-text, modeled on **Mood**. Known set: **None, Stationary, Walking, Eating, Commuting,
  Exercising, Resting**. `Resolve(string)` matches known members case-insensitively and falls back to
  `Custom` carrying the User's raw words; empty/absent → `None`. **Sole persisted state is the
  canonical `string Value`** — known-ness and icon key are derived on demand, never stored. `None`
  means "didn't say / N/A" and is distinct from `Stationary` ("deliberately sitting still"). There is
  **no `null`/unset state**.
- **`Session`** gains a non-null `Activity` (default `None`) and `SetActivity`. **`UserSet` only** —
  no provenance, never an AI **Suggestion**, untouched by **Cleanup**.
- **Persistence.** Map via EF Core 10 **`ComplexProperty`** to an `activity` column (no
  `ValueConverter` — `Value` is the only mapped scalar). Extract the Session mapping into a new
  `SessionConfiguration : IEntityTypeConfiguration<Session>` registered via
  `ApplyConfigurationsFromAssembly`; the instance-dependent tenant `HasQueryFilter` stays in
  `OnModelCreating` for now (the marker-driven helper lands in #0030). Regenerate the single initial
  migration (drop the dev DB first); no backfill.
- **Write contract → full-replace** (ADR-0011). `MetadataForWrite` becomes a complete, non-partial
  payload (`Topics`, `Moods`, **`Activity`**); `UpdateMetadata` replaces all metadata wholesale,
  retiring the nullable-means-don't-touch convention. Same `PUT /api/sessions/{id}/metadata` endpoint;
  Suggestion-accept and Person-proposal flows are unaffected.
- **DTO + web types** gain `Activity` (string).
- **UI.** A single-select Activity picker in the metadata editor — `None` + known members + custom
  free-text entry, with a recognizable icon per member (e.g. a couch for `Stationary`). The chosen
  Activity renders on the session card/detail.

## Acceptance criteria

- [ ] A new Session has Activity `None` with no user action.
- [ ] The User can set Activity to any known member or a custom free-text value, and change it at any
      time after creation.
- [ ] A known activity persists and reads back by its canonical name; a custom activity persists and
      reads back as its raw free-text (never the literal `"Custom"`).
- [ ] Selecting `None` in the picker represents "no particular activity" and is distinct from
      `Stationary`.
- [ ] `PUT /api/sessions/{id}/metadata` accepts a complete `{ Topics, Moods, Activity }` payload and
      replaces all three wholesale; the nullable-don't-touch behavior is gone.
- [ ] The AI never sets or suggests Activity; Cleanup leaves it untouched.
- [ ] The chosen Activity is visible on the session card/detail with its icon.
- [ ] Session persistence is configured through `SessionConfiguration : IEntityTypeConfiguration<Session>`
      and the Activity column is mapped via `ComplexProperty` (no `ValueConverter`).
- [ ] **Unit tests** cover `Activity.Resolve`: known case-insensitive match, unknown → `Custom(text)`,
      empty → `None`, and `None` as the default/zero distinct from `Stationary`.
- [ ] **Functional test**: a metadata `PUT` round-trips Activity (known and custom) and replaces
      Topics/Moods/Activity wholesale.

## Blocked by

- None — can start immediately.
