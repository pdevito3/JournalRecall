# FE-028 — Committed dev-browser helper module (`login`, `completeSetup`, base-URL)

**Phase:** 12 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0005

## What to build

Add a committed **dev-browser** helper module as the single source of truth for e2e flows: `login`,
`completeSetup` (handles the first-run **setup** gate), and base-URL/port resolution (the Vite port
under `/app`). Each flow declares its precondition — fresh DB → run setup, or seeded → log in with a
unique-per-run identity — so re-runs don't fail on "username taken" or land on the wrong page.

Helpers locate elements by role/label/text (`getByRole`, `getByLabel`, `getByRole('alert')`) and use
web-first auto-retrying assertions, not `sleep`/`networkidle`. Use `exact` label matching on the
multi-password setup/register/change-password forms and scope locators to a region where roles repeat.
Stub or skip third-party surfaces (geolocation for **Location**, AI-provider responses).

**Decisions (record in the issue/PR):** whether the dev-browser QuickJS sandbox exposes Playwright's
`expect` matchers (a ~2-minute spike); where the e2e helpers/scripts live and that they're committed;
the canonical "reset to known state" command (delete the per-worktree SQLite DB vs. unique-per-run
identities).

## Acceptance criteria

- [ ] A committed helper module exposes `login`, `completeSetup`, and base-URL/port resolution, using
      role/label locators + web-first assertions (no `sleep`/`networkidle`).
- [ ] Each helper documents its precondition (fresh-DB setup vs. seeded login with unique-per-run
      identity) and handles the first-run setup gate.
- [ ] The three decision points (sandbox `expect`, helper location, reset command) are recorded; a
      sample flow runs green against a local app.

## Blocked by

- None - can start immediately
