// Committed dev-browser e2e helper module (FE-028) — the single source of truth for e2e flows.
//
// HOW IT'S CONSUMED. dev-browser runs scripts in a QuickJS sandbox with NO import/require (see
// `dev-browser --help`). So composition happens at the shell: this file is concatenated AHEAD of a
// flow script before being piped to dev-browser. Use the committed runner:
//
//     e2e/run.sh <flow-file> [port]
//
// which injects the port and prepends these helpers. Everything here is declared at top level
// (functions + consts) so the flow script that follows can call it directly. No exports.
//
// RECORDED DECISIONS (FE-028):
//   1. Sandbox `expect`: the QuickJS sandbox does NOT expose Playwright's `expect` matchers
//      (`typeof expect === 'undefined'`). So web-first waiting here uses the real auto-retrying
//      LOCATOR methods that ARE available — `locator.waitFor({ state })` and `page.waitForURL()` —
//      not `expect(locator).toBeVisible()`. A generic value-polling `waitFor` shim is added in FE-030.
//   2. Helper location: committed at `src/JournalRecall.Api/web/e2e/` (next to the web app), run via
//      `e2e/run.sh`. Flows live in `e2e/flows/`.
//   3. Reset to known state: the SQLite DB (`journalrecall.db`) is per-worktree (see docs/dev-runbook.md).
//      A FRESH-DB flow deletes it so the app shows the first-run setup gate; a SEEDED flow logs in with a
//      unique-per-run identity. Delete the DB with `e2e/reset-db.sh` BEFORE a fresh-setup run.

// --- base URL / port resolution -------------------------------------------------------------------
// The Vite SPA is served under the `/app` base path. Isolated mode randomizes the port; the runner
// injects it as `globalThis.JR_PORT`. Falls back to the fixed standalone web port (4247).
function jrPort() {
  return (typeof globalThis !== 'undefined' && globalThis.JR_PORT) || 4247
}

function baseUrl(path) {
  const p = !path || path === '/' ? '/' : path.startsWith('/') ? path : '/' + path
  return `http://localhost:${jrPort()}/app${p}`
}

async function gotoApp(page, path) {
  // No `networkidle`: navigate then let web-first locator waits gate readiness (Playwright anti-pattern
  // to wait on the network; wait on the UI instead).
  await page.goto(baseUrl(path))
}

// --- unique-per-run identity ----------------------------------------------------------------------
// So re-runs of a seeded flow don't fail on "username taken". The runner may inject `globalThis.JR_RUN`
// (a per-invocation id); otherwise fall back to a timestamp.
function runId() {
  if (typeof globalThis !== 'undefined' && globalThis.JR_RUN) return String(globalThis.JR_RUN)
  return 'r' + Date.now()
}

function uniqueUsername(prefix) {
  return `${prefix || 'user'}_${runId()}`
}

// A password that satisfies the shared passwordSchema policy (FORM-004).
const DEFAULT_PASSWORD = 'Sup3rSecret!pw'

// commitFields — the ONE gotcha of driving these forms (FE-028). Validation is `onBlur` and the submit
// button is gated on react-form's `canSubmit`. Filling fields one-by-one blurs each *previous* field, so
// onBlur validation keeps firing against a snapshot where the not-yet-filled fields are still empty — the
// form stays invalid and the button stays `disabled`, so a click would just retry until timeout. The fix
// is a single blur *after every field is filled*, so one last validation runs against the complete, valid
// form and `canSubmit` flips true. Call this on the last-filled field, before clicking submit.
async function commitFields(lastField) {
  await lastField.blur()
}

// --- flows ----------------------------------------------------------------------------------------

// completeSetup — PRECONDITION: FRESH DB (run e2e/reset-db.sh first). Handles the first-run setup gate:
// creates the root Admin and lands on the journal. Returns the credentials it created.
async function completeSetup(page, opts) {
  const username = (opts && opts.username) || uniqueUsername('admin')
  const password = (opts && opts.password) || DEFAULT_PASSWORD

  await gotoApp(page, '/setup')
  // `exact` label matching: "Password" must not also match "Confirm password" on this multi-password form.
  await page.getByLabel('Username', { exact: true }).fill(username)
  await page.getByLabel('Password', { exact: true }).fill(password)
  const confirm = page.getByLabel('Confirm password', { exact: true })
  await confirm.fill(password)
  await commitFields(confirm) // blur the last field so onBlur validation enables the submit button
  await page.getByRole('button', { name: 'Create admin account' }).click()

  // Setup signs in and routes to the journal; wait on the URL + a known control rather than a sleep.
  await page.waitForURL((url) => !/\/(setup|login)\b/.test(String(url)))
  await page.getByRole('button', { name: 'Start a session' }).waitFor({ state: 'visible' })
  return { username, password }
}

// login — PRECONDITION: SEEDED DB with a known identity (e.g. one created by completeSetup, or a
// unique-per-run account). Signs in and lands on the journal.
async function login(page, opts) {
  const username = opts && opts.username
  const password = (opts && opts.password) || DEFAULT_PASSWORD
  if (!username) throw new Error('login(page, { username, password }) requires a username')

  await gotoApp(page, '/login')
  await page.getByLabel('Username', { exact: true }).fill(username)
  const pw = page.getByLabel('Password', { exact: true })
  await pw.fill(password)
  await commitFields(pw) // blur the last field so onBlur validation enables the submit button
  await page.getByRole('button', { name: 'Sign in' }).click()

  await page.waitForURL((url) => !/\/login\b/.test(String(url)))
  await page.getByRole('button', { name: 'Start a session' }).waitFor({ state: 'visible' })
  return { username, password }
}

// startSession — PRECONDITION: signed in, on a page with the "Start a session" button (the journal home).
// Creates a Session, lands on its editor, optionally writes `text` into the Raw draft and waits for the
// debounced autosave to report "Saved". Returns the new session id (parsed from the URL).
async function startSession(page, text) {
  await page.getByRole('button', { name: 'Start a session' }).click()
  await page.waitForURL((url) => /\/sessions\/[^/]+/.test(String(url)))
  const id = String(page.url()).replace(/[?#].*$/, '').split('/sessions/')[1]

  const draft = page.getByPlaceholder('Write freely…') // the Raw editor textarea
  await draft.waitFor({ state: 'visible' })
  if (text) {
    await draft.fill(text)
    // Debounced autosave (600ms) → the SaveStatus flips to "Saved". Wait on that, not a sleep.
    await page.getByText('Saved', { exact: true }).first().waitFor({ state: 'visible' })
  }
  return id
}

// openFromTimeline — PRECONDITION: on the journal home. Clicks the timeline entry whose preview contains
// `marker` (a per-run text written into that Session), landing on that Session's editor. This is how a
// user reaches a Session, and it exercises client-side navigation + the route loader (FE-007/008).
async function openFromTimeline(page, marker) {
  await page.getByRole('link', { name: new RegExp(marker) }).first().click()
  await page.waitForURL((url) => /\/sessions\/[^/]+/.test(String(url)))
  await page.getByPlaceholder('Write freely…').waitFor({ state: 'visible' })
}

// Convenience: the form-level error banner text (Form.Errors renders role="alert"). Scope to a region
// when roles repeat on a page.
async function bannerText(scope) {
  const alert = scope.getByRole('alert')
  if ((await alert.count()) === 0) return ''
  return (await alert.first().innerText()).trim()
}

// expectText — poll until `locator` is present AND its text contains `needle`, then return the full text.
// Built on the FE-030 `waitFor` shim (the sandbox has no `expect`): unlike `locator.waitFor`, this gates
// on the element's *content*, which is what flows asserting "heading B shows B's text" actually need.
async function expectText(locator, needle, opts) {
  const message = (opts && opts.message) || `text "${needle}"`
  return waitFor(
    async () => {
      if ((await locator.count()) === 0) return false
      const text = (await locator.first().innerText()) || ''
      return text.includes(needle) ? text : false
    },
    { message: message, timeout: opts && opts.timeout },
  )
}

// expectValue — like expectText but for form controls (input/textarea), polling `inputValue()` instead of
// innerText. Used by flows that assert a textarea/field shows the right *value* (e.g. each Session's own
// Raw draft). Built on the FE-030 `waitFor` shim.
async function expectValue(locator, needle, opts) {
  const message = (opts && opts.message) || `value "${needle}"`
  return waitFor(
    async () => {
      if ((await locator.count()) === 0) return false
      const value = (await locator.first().inputValue()) || ''
      return value.includes(needle) ? value : false
    },
    { message: message, timeout: opts && opts.timeout },
  )
}

// captureFailure — on a failed flow, save a full-page screenshot and print the role="alert" banner text,
// so a failing run leaves enough to diagnose without a re-run (FE-030). Best-effort: never throws.
async function captureFailure(page, name) {
  try {
    await saveScreenshot(await page.screenshot({ fullPage: true }), `fail-${name}.png`)
    console.log(`captured screenshot: fail-${name}.png (in ~/.dev-browser/tmp/)`)
  } catch (error) {
    console.log('screenshot capture failed:', String(error))
  }
  try {
    const banner = await bannerText(page)
    if (banner) console.log(`role=alert: ${banner}`)
  } catch (error) {
    // ignore — banner is a diagnostic nicety
  }
}

// runFlow — the standard flow wrapper (FE-030): opens a named page, runs the body, prints `NAME: PASS`,
// and on any throw captures a screenshot + alert text then re-throws (so the runner still exits non-zero).
async function runFlow(name, body) {
  const page = await browser.getPage(name)
  try {
    await body(page)
    console.log(`${name.toUpperCase()}: PASS`)
  } catch (error) {
    console.log(`${name.toUpperCase()}: FAIL — ${String(error)}`)
    await captureFailure(page, name)
    throw error
  }
}
