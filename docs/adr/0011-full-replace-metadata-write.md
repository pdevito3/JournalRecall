# Session metadata is written as a full-replace PUT, not a nullable partial update

## Status

accepted — realized by [PRD-0007](../prd/0007-session-activity-metadata.md)

## Context & decision

Writing a Session's **Metadata** (`Topics`, `Moods`, and now **Activity**) went through
`PUT /api/sessions/{id}/metadata` with a `MetadataForWrite` whose fields were **nullable**, where the
handler read `null` as "leave this field alone" — a pseudo-PATCH wearing a PUT's clothes. That
convention has two problems: it overloads `null` (is a missing field "clear it" or "ignore it"?), and
it has **no honest place for a non-nullable field** like Activity, which is always present and defaults
to `None`.

The only caller of this endpoint is the metadata editor form, which **already holds the complete
metadata object** — Topics, Moods, and Activity together. The other mutators of Topics/Moods (accepting
an AI **Suggestion**, approving a **Person** proposal) go through their **own** endpoints, not through
`MetadataForWrite`. So no caller ever updates a *subset* of metadata in isolation.

Therefore: **`MetadataForWrite` becomes a complete, non-partial payload and the handler replaces all
metadata wholesale.** PUT regains its honest idempotent-replace meaning; `null` is no longer
overloaded; Activity sits naturally as a required canonical string alongside the Topic/Mood lists.

## Considered options

- **Typed presence-wrapper patch (`Optional<T>`/`Patch<T>` per field).** Honest partial updates
  without overloading `null`, via a custom `JsonConverter`. Rejected for now: it adds machinery that is
  only justified by a caller that updates part of the metadata in isolation, and there is none.
- **JSON Patch (RFC 6902, `[{op,path,value}]`).** Standard, but verbose and awkward in this
  minimal-API/MediatR style and overkill for three fields. **Door left open** as a future expansion if
  genuine partial-update callers emerge — see Consequences.
- **Keep nullable-means-don't-touch.** Rejected: overloads `null` and cannot express a non-nullable
  Activity.

## Consequences

- The client always sends the whole metadata object; a partial payload is not a supported shape.
- If a future caller genuinely needs to mutate part of the metadata independently of the form, the
  intended evolution is a **JSON Patch** contract on this endpoint, not a return to nullable
  "don't-touch" fields. This ADR records that as the sanctioned next step rather than a silent reversal.
- Accepting a Suggestion and approving a Person proposal are unaffected — they keep their own endpoints.
