# Dev runbook: isolated-mode app + dev-browser

How to run the app on throwaway ports (so multiple worktrees / the root can run at
once) and drive its UI end-to-end with `dev-browser`.

## Aspire isolated mode

```bash
aspire run --isolated      # randomized ports + isolated user secrets → multiple instances at once
aspire run                 # standalone, fixed ports (API 5247/7247, web 4247)
```

Works the same from a worktree or the non-worktree root. Use `--isolated` whenever
another instance might already be up (another worktree, or the root) so they don't
collide on ports.

### Finding the URLs

Isolated ports are random and the CLI only prints the dashboard URL. List the
listeners:

```bash
lsof -nP -iTCP -sTCP:LISTEN | grep -iE 'node|dotnet'
```

- `node …:<port>` → the Vite SPA. **Browser URL is `http://localhost:<port>/app/`**
  (note the `/app` base path).
- `dotnet …` → the API (https) + Aspire dashboard. You rarely hit these directly.

### Why a single URL is enough

The Vite dev server proxies `/api/*` to the API and follows Aspire's injected
`services__api__http__0`, so it targets whatever random port the API was allocated
(see `vite.config.ts`). Everything is one **http** origin — no TLS/cert friction.

Health check:

```bash
curl http://localhost:<nodePort>/api/health      # expect 200
```

### Data store

`journalrecall.db` (SQLite) lives in the API project dir, so it is **per-worktree** —
each worktree has its own data; they don't share. Within one worktree, repeated runs
reuse the same file (isolated mode randomizes ports/secrets, not the DB).

### Teardown

`Ctrl+C` should stop the run. The AppHost has been observed to ignore SIGINT; if a
graceful stop leaves it running, kill the worktree's process tree by path and confirm
the ports are freed. Leave PlateWise / any pre-existing `dcp` processes alone.

```bash
pkill -f 'JournalRecall-worktrees/<name>/JournalRecall.AppHost'
pkill -f 'JournalRecall-worktrees/<name>/src/JournalRecall.Api/bin'
lsof -nP -iTCP:<ports> -sTCP:LISTEN               # expect empty
```

## dev-browser

[`dev-browser`](https://github.com/SawyerHood/dev-browser) is a sandboxed CLI (not an
MCP). One-time setup:

```bash
npm install -g dev-browser
dev-browser install        # Playwright Chromium
```

Scripts run in a QuickJS sandbox: **no Node APIs and no `import`/`require`**, and
Playwright's **`expect` matchers are not exposed** (`typeof expect === 'undefined'`).
But `browser` is a real Playwright handle returning real `Page` objects, so the
auto-retrying **locator** APIs (`getByRole`, `getByLabel`, `locator.waitFor`,
`page.waitForURL`) are all available — use those instead of `expect`.

### Run flows through the committed helper module (start here)

Login/setup logic lives once in the committed e2e helper module
(`src/JournalRecall.Api/web/e2e/`, FE-028). Don't re-type it in ad-hoc scripts — write a
flow under `e2e/flows/` and run it with the runner, which injects the port and, because
the sandbox has no `import`, concatenates the helpers (and the `waitFor` shim) ahead of
your flow:

```bash
cd src/JournalRecall.Api/web

# Reset to a known FRESH state (stop the app first — EF holds the DB file open):
e2e/reset-db.sh

# Run a flow against the isolated Vite port (find it with the lsof command above):
e2e/run.sh e2e/flows/auth-smoke.js <nodePort>
```

A flow composes the helpers — `completeSetup` (fresh-DB first-run gate), `login`
(seeded, unique-per-run identity), `gotoApp`, `bannerText`:

```js
// e2e/flows/my-flow.js  (helpers are already in scope — no import)
const page = await browser.getPage('my-flow')
const creds = await completeSetup(page)            // fresh DB → creates root admin, lands on journal
await login(page, creds)                           // seeded → signs back in
console.log('OK')
```

### Locate by role/label + web-first waits (the default)

The UI is accessible react-aria, so role/label locators are free and stable — prefer
them over CSS selectors. To wait, use the auto-retrying locator/URL waits, **not**
`sleep`/`waitForLoadState('networkidle')` (a Playwright anti-pattern — wait on the UI,
not the network):

```js
await page.goto(baseUrl('/login'))                                 // helper builds the /app URL
await page.getByLabel('Username', { exact: true }).fill('admin')   // role/label, not input[name=…]
await page.getByRole('button', { name: 'Sign in' }).click()
await page.waitForURL((url) => !/\/login\b/.test(String(url)))      // web-first: wait on the URL
await page.getByRole('button', { name: 'Start a session' }).waitFor({ state: 'visible' })
const err = await bannerText(page)                                 // the role="alert" form banner
```

Conventions that matter for this app's forms:

- **Validation is `onBlur` and submit is gated on `canSubmit`.** Filling fields
  one-by-one blurs each *previous* field, so validation keeps running against a snapshot
  where later fields are still empty and the button stays `disabled`. Fill every field,
  then blur the **last** one (`locator.blur()`), *then* click submit. The `completeSetup`
  /`login` helpers already do this (`commitFields`); replicate it in any new form helper.
- **`exact` label matching on multi-password forms.** Setup/register/change-password show
  both `Password` and `Confirm password`, so `getByLabel('Password')` is ambiguous — pass
  `{ exact: true }`.
- **Scope locators to a region where roles repeat.** Query within a section/form
  (`page.getByRole('form')…` or a scoped locator) so controls elsewhere on the page don't
  collide.
- **Form banner is `getByRole('alert')`** (rendered by `Form.Errors`); field errors render
  inline. Use the `bannerText(scope)` helper to read the banner.

### Third-party surfaces: stub or skip

- **Geolocation (Location on "Start a session").** `captureLocation()` asks the browser for
  one point and resolves `undefined` if unsupported/denied/timeout, so a session still
  starts without it — you can ignore it. Note the 10s timeout: don't grant geolocation in a
  flow unless you're testing the located path; the un-granted path resolves fast on denial.
- **AI provider / summaries.** Summary generation calls the server-configured AI provider
  (external, slow, needs a key). Don't drive real summary generation in e2e — skip those
  flows, or seed/stub the provider config server-side first.

### Misc

- Screenshots: `saveScreenshot(await page.screenshot({ fullPage: true }), 'shot.png')` →
  lands in `~/.dev-browser/tmp/`.
- `dev-browser stop` kills the daemon + Chromium. `dev-browser --help` has the full API.
- To skip permission prompts in Claude Code, add `Bash(dev-browser *)` to the settings
  `allow` list.

### Last-resort fallback (avoid)

Only if a control is genuinely unreachable by role/label: CSS selectors and timed waits
still work, but they're brittle and **not** the house style.

```js
await page.locator('input[name="username"]').fill('admin')   // name={field.name}; prefer getByLabel
await page.locator('.text-red-400').allInnerTexts()          // field errors; prefer getByRole('alert')
await page.waitForTimeout(500)                                // last resort; prefer locator.waitFor
```

react-aria Selects are a trigger button + `div[role="option"]` items (no native
`<select>`) — prefer `getByRole('button')` to open and `getByRole('option', { name })`
to pick, scoped to the form's region.
