# JournalRecall — Context

A simple, self-hosted journaling application: a user starts a journaling **Session**, writes
or dictates freely, and can optionally run an AI cleanup pipeline that produces a polished copy
without ever altering the original. The glossary below is the source of truth for the domain
language. Implementation rationale lives in [`docs/adr/`](docs/adr/); the phased build-out is in
[`docs/ROADMAP.md`](docs/ROADMAP.md).

## Language

**Session**:
The core aggregate. One act of journaling, started by a User on a given day (a day may have
multiple). Owns the Raw text the User wrote, any AI-derived Cleaned copy, the Synopsis, and
metadata. You start it, write into it, and may run AI over it at any time — there is no
complete/lifecycle step.
_Avoid_: Entry (do not use as a separate noun — the Session *is* the entry)

**Raw** (raw content):
The text the user wrote or dictated — the human-owned source of truth. Editable by the user at
any time; **never** altered by AI.
_Avoid_: Original (the raw text changes over time, so it isn't a fixed "original")

**Cleaned** (cleaned content):
The AI-derived, polished copy produced by the cleanup pipeline from the Raw text. The user may
also hand-edit it. A re-run regenerates it (warn-and-overwrite, prior version kept in history).
_Avoid_: Edited, Final, Processed

**Stale**:
State of the Cleaned copy when the Raw text has changed since the last AI run — the cue to offer
a re-run. Distinct from a user hand-edit of the Cleaned copy, which is intentional polish.

**Revision**:
An immutable, appended snapshot of content. A new Revision is minted at save points — AI-run
completion and explicit/debounced edit saves — not per keystroke. Raw and Cleaned each have their
own Revision stream. Everything else (status, metadata) is ordinary mutable state.
Revisions are a **per-Session/per-day drill-down**, viewed within a Session — they are **not part
of the searchable index**. Browsing/filtering the journal queries only each Session's **current**
state; historical Revisions never appear as separate results.

**Draft**:
The in-progress, autosaved working text of a live Session before it crystallizes into a Revision
at a save point. Drafts mutate; Revisions append.

> Note: A Session has **no** `InProgress`/`Completed` lifecycle — it simply exists, and AI cleanup
> may be run or re-run at any time.

**Cleanup** (AI cleanup):
The pipeline that reads a Session's Raw text and produces the Cleaned copy, the **Synopsis**, and
AI metadata Suggestions (formatting, typo/context-word fixes, etc.). Runs manually (a button) in
v1; an optional per-user nightly batch over Stale/never-run Sessions comes later. Running Cleanup
marks the Session's affected period **Summaries** `Stale`. Cleanup is **one concept with two
Engines** — where the model runs never changes what a Cleanup *is* or what it produces.
_Avoid_: Processing, evaluation (informal "AI eval" is fine in conversation, not as a domain term)

**Engine** (Cleanup engine):
Where a Cleanup's model executes: `Server` (the app-wide Admin-configured provider, via the
existing cleanup endpoints) or `OnDevice` (Apple Foundation Models on the user's iPhone, result
submitted back to the server and recorded exactly like a server run). The user picks a default
Engine and may override per run. Regardless of Engine, the server performs the same
post-processing and the outcome is indistinguishable in the domain.
_Avoid_: Runner, Provider (Provider is the Admin's server-side model config, not the run location)

**Synopsis**:
The short AI recap of a **single** Session, written onto that Session by Cleanup. Distinct from a
**Summary** (which is a period roll-up).

**Cleanup status**:
Where a Session stands with AI: `NotRun | Running | Clean | Stale | Failed`. Mostly derived —
`Stale` means the latest Raw Revision is newer than the last successful Cleanup.

**Corrections**:
A per-user list of known terms used to fix mis-dictations during Cleanup. Each **Correction** has
a canonical term (`Profisee`) and common mishearings (`prophecy`…). Default mode is an AI-context
hint (the model fixes in-context); an entry may be flagged **hard-replace** for deterministic
substitution. Applied only to the Cleaned copy — Raw is never touched.
_Avoid_: Lexicon, Vocabulary, Dictionary

## Metadata

A Session carries metadata. Each piece of metadata records **provenance**: `UserSet` or
`AiSuggested`. AI never overwrites user-set metadata — it offers **Suggestions** the user accepts
or rejects; accepted suggestions become regular metadata.

**Topic**:
A life-area tag on a Session (work, parenthood, relationships, trips…). **Per-user data**,
user-extensible — not a fixed list. A Session may have many.

**Person**:
A person referenced in a Session. **Per-user data** (free-form names the user or AI accumulate).
A Session may reference many.

**Mood**:
How the user felt, as a value object: a known mood from an app-defined **SmartEnum**, or the
`Custom` member which additionally carries the user's free-text mood value.

**Activity**:
What the user was physically doing *while journaling* — a value object, **exactly one per
Session** (single-valued, unlike Mood/Topic). A known activity from an app-defined **SmartEnum**,
or the `Custom` member which additionally carries the user's free-text value. **Non-nullable**:
every Session always has an Activity, defaulting to `None`. The `None` member means "didn't say /
not applicable" and is the zero value — there is no separate `null`/unset state. Distinct from
`Stationary`, which means the user was deliberately sitting still. Describes the *act of
journaling* (posture/motion), **not** the content — life-areas are Topics, feelings are Moods.
_Avoid_: using `None` and "unset" as two different things (there is only `None`).

**Suggestion**:
An AI-proposed piece of metadata (a Topic, Person, or Mood) awaiting the user's accept/reject. Not
yet authoritative metadata.

## Summaries

**Summary**:
An AI-generated narrative over a time **period** for one user, keyed by (user, period, date). The
period is one of `Day | Week | Month | Quarter | Year`. Distinct from a **Synopsis** (single
Session). Generated on demand from the summary page (and optionally as part of an AI-eval flow); no
scheduler in v1 — a nightly batch is a later enhancement. Roll-up is **hybrid**:
- **Day** and **Week** summarize the underlying Sessions directly (reading the Cleaned copy when
  present, else Raw). Week is a parallel roll-up of its days' Sessions, sitting outside the month
  chain (a week can span two months).
- **Month** summarizes its Day Summaries; **Quarter** summarizes its Months; **Year** summarizes
  its Quarters.

A Summary goes **stale** when anything beneath it changes; staleness propagates up the chain.

## Other

**Location**:
An optional single geo-point (lat/long) stamped on a Session at creation. Per-user opt-in
(default off), declinable per Session. Coordinates only; a place label is deferred.

## Identity & Access

**User**:
The tenant boundary. All journal data (Sessions, Corrections, metadata, Summaries) belongs to
exactly one User and is **strictly private to that User** — see the Privacy invariant.

**Admin**:
A User role granting **non-journal** administration only: manage users (invite/disable/assign
roles), app-wide settings (AI provider/model, external OIDC providers), and system
health/telemetry. An Admin has **zero** visibility into any User's journals — no override.

**Member**:
A User role with full use of their own journal and no admin surface. Default role.

**Privacy invariant**:
No User — Admin included — can ever read another User's journal data. Tenancy is by User, and the
wall is absolute.

**Journaling day**:
The calendar day a Session belongs to, computed by projecting its UTC timestamp into the **User's
configured timezone**. The unit for "sessions for a day" and Day/Week/Month grouping. Sessions
store absolute UTC; day/week/month membership is derived.

## Example dialogue

> **Dev:** When the AI "fixes" an entry, does it edit what I typed?
> **Domain expert:** Never. The **Raw** text is yours — AI only ever reads it. Cleanup writes a
> separate **Cleaned** copy.
> **Dev:** And if I tweak that Cleaned copy by hand, then change my Raw words and re-run?
> **Domain expert:** Re-running warns you first, then overwrites the Cleaned copy — but the old one
> is kept as a **Revision**, so nothing's lost. Your Raw edit is what made the Cleaned copy
> **Stale** in the first place.
> **Dev:** It kept dictating "prophecy" instead of my company "Profisee."
> **Domain expert:** Add a **Correction** — canonical "Profisee", mishearing "prophecy". Cleanup
> applies it to the Cleaned copy. Flag it hard-replace if you want it substituted every time.
> **Dev:** The page says "work" on this entry — did I tag that?
> **Domain expert:** Check the provenance. If it's `AiSuggested` it's still just a **Suggestion**
> until you accept it; your own **Topic** tags are `UserSet` and AI won't touch them.
> **Dev:** Is the "summary of my week" the same kind of thing as the recap on one entry?
> **Domain expert:** No — the per-entry recap is a **Synopsis**. The week/month/year roll-up is a
> **Summary**. Different concepts; don't conflate them.
> **Dev:** I'm an admin — can I read another user's entries to debug?
> **Domain expert:** No. The **Privacy invariant** is absolute: no one, admin included, ever sees
> another **User's** journal.
