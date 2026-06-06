# FE-020 — Extract the Session-detail route into a feature component

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

The Session detail route is ~440 lines of editor state machine. Extract that logic into a testable
feature component so the route becomes a thin `createFileRoute` + render shell that delegates. Build on
the `key`-remount fix (FE-013) so the extracted component is the natural home for the now-simplified
editor state.

## Acceptance criteria

- [ ] The Session-detail editor logic lives in a feature component; the route file is a thin shell
      (`createFileRoute` + loader + render of the feature component).
- [ ] No behavior change to the editor; a dev-browser pass confirms create/view/edit still work.
- [ ] App boots and existing tests stay green.

## Blocked by

- FE-013
