# 0011 — Manual metadata (Topics, People, Mood) + filtering

**Phase:** 5 · **Type:** AFK · **Status:** todo

## What to build

Let users manually tag a Session with **Topics**, **People**, and a **Mood**, and filter the
timeline by them. Every tag records provenance `UserSet`.

- **Topic** and **Person** as per-user data (user-extensible; a Session may have many of each).
- **Mood** as a value object: an app-defined SmartEnum of known moods, plus a `Custom` member that
  carries a free-text value.
- All manual tags carry provenance `UserSet`.
- QueryKit filtering of the timeline by Topic, Person, and Mood.
- React: per-Session metadata editor; filter controls on the timeline.

## Acceptance criteria

- [ ] A user can add/remove Topics and People (their own list) on a Session, and set a Mood
      (including `Custom` with free text).
- [ ] Manually set metadata is stored with provenance `UserSet`.
- [ ] The timeline can be filtered by Topic, by Person, and by Mood (QueryKit), returning the
      correct Sessions.
- [ ] Topics/People are per-user — one user's lists are not visible to another.
- [ ] Mood `Custom` round-trips its free-text value.

## Blocked by

- #0004
- #0006
