# Reference-flow catalog (FE-030)

Named dev-browser flows for the common journeys, composed from the shared helpers in
`../helpers.js` (driven by `../run.sh`, which prepends `../shim.js` + `../helpers.js`).
See `docs/dev-runbook.md` for the locator/wait conventions and the `onBlur`/`canSubmit`
gotcha these helpers encapsulate.

Each flow runs through `runFlow(name, body)`, so a failure prints `NAME: FAIL — …`,
captures a full-page screenshot (`~/.dev-browser/tmp/fail-<name>.png`), and echoes the
`role="alert"` banner text before re-throwing.

| Flow | Precondition | Proves |
| --- | --- | --- |
| `auth-smoke.js` | **fresh DB** | first-run setup gate + a fresh login land on the journal |
| `journal-session.js` | **fresh DB** | start a session → write → debounced autosave ("Saved") → draft survives a reload (route loader re-fetch, FE-007/008) |
| `session-isolation.js` | **fresh DB** | two sessions each show their OWN raw draft — no cross-leak (FE-013 `key={sessionId}` remount) |

## Running

```bash
cd src/JournalRecall.Api/web

# fresh-DB flows: stop the app, reset, restart, then run (the DB file is held open while running)
e2e/reset-db.sh
# … (re)start the isolated app, find its Vite port via lsof — see docs/dev-runbook.md …
e2e/run.sh e2e/flows/auth-smoke.js <port>
```

Each fresh-DB flow runs `completeSetup`, so run them one-per-fresh-DB (reset between
them) rather than back-to-back against the same instance.
