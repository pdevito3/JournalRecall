// Generic value-polling `waitFor` shim (FE-030). The dev-browser QuickJS sandbox does NOT expose
// Playwright's `expect` matchers (FE-028 decision #1), and `locator.waitFor`/`page.waitForURL` only gate
// on a SINGLE locator's state or the URL. This shim covers the gap: it polls an arbitrary async predicate
// until it returns a truthy value (or times out), so flows can wait on COMPUTED conditions the built-in
// waits can't express — cross-locator comparisons, derived text, API/JSON state, "settled" counts, etc.
//
// LOADED FIRST. `run.sh` concatenates this ahead of `helpers.js`, so `waitFor`/`retry` are in scope for
// every helper and flow. No import/exports (the sandbox has neither).
//
// Prefer the built-in web-first waits when they fit (one locator's visibility, a URL change); reach for
// `waitFor` only for predicates that span more than one element or a non-DOM source.

// sleep — the sandbox exposes a real `setTimeout` (verified during the FE-030 spike), so this resolves
// after `ms`. Used only for the poll interval.
function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms))
}

// waitFor(predicate, opts) — poll `predicate()` (sync or async) until it returns a truthy value, then
// return that value. Throws after `timeout` ms with the last error/label. A throwing predicate counts as
// "not yet" (so you can write `await locator.innerText()` style predicates without guarding races).
//   opts: { timeout = 10000, interval = 100, message = 'condition' }
async function waitFor(predicate, opts) {
  const timeout = (opts && opts.timeout) || 10000
  const interval = (opts && opts.interval) || 100
  const label = (opts && opts.message) || 'condition'
  const deadline = Date.now() + timeout
  let lastError
  for (;;) {
    try {
      const value = await predicate()
      if (value) return value
    } catch (error) {
      lastError = error
    }
    if (Date.now() >= deadline) {
      const suffix = lastError ? ` (last error: ${String(lastError)})` : ''
      throw new Error(`waitFor: ${label} not met within ${timeout}ms${suffix}`)
    }
    await sleep(interval)
  }
}

// retry(fn, opts) — like waitFor but returns the resolved value of `fn` once it stops throwing (instead
// of polling for a truthy return). Handy for "do X until it doesn't error" without a separate predicate.
async function retry(fn, opts) {
  return waitFor(
    async () => {
      const value = await fn()
      return value === undefined || value === null ? true : value
    },
    opts,
  )
}
