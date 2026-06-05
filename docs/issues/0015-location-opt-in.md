# 0015 — Location opt-in

**Phase:** 7 · **Type:** AFK · **Status:** done

## What to build

Optional geotagging: when a user opts in, a single geo-point (lat/long) is captured at Session
creation. Off by default and declinable per session. Coordinates only.

- Per-user **geo opt-in** setting (default off).
- When enabled, capture a single lat/long from the browser geolocation API at Session creation and
  store it on the Session; the user may decline for a given Session.
- React: opt-in setting; capture prompt/affordance at session start; display coordinates on the
  Session.

## Acceptance criteria

- [ ] With opt-in off (default), no location is captured or requested.
- [ ] With opt-in on, creating a Session captures and stores one lat/long; declining leaves it empty.
- [ ] A stored location is a single point (no track) and shows on the Session view.
- [ ] The setting is per-user and does not affect other users.

## Blocked by

- #0004
