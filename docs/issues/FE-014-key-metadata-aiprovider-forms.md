# FE-014 — Key the Metadata editor + Admin AI-provider form on entity identity

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

Key the **Metadata** editor and the **Admin** AI-provider form on their entity identity (plus a
change-token where one exists) so refetched server values re-seed the form instead of going stale —
e.g. after an accepted **Suggestion** or a **Cleanup** re-run, the editor shows the new server values
rather than the values captured at first mount. Prefer render-time derivation / `key`-based remount
over `useEffect` + `setState`.

## Acceptance criteria

- [ ] The Metadata editor and the AI-provider form re-seed when their underlying server entity changes
      (keyed on identity + change-token where available).
- [ ] No `useEffect`-that-`setState`s-from-query-data remains in these two forms.
- [ ] A manual/dev-browser pass confirms an accepted Suggestion (Metadata) and a saved provider change
      re-seed the respective form.

## Blocked by

- None - can start immediately
