// Sample flow (FE-028): fresh-DB setup, then a fresh login with the same identity, proving the
// committed helpers drive the first-run gate and sign-in against a local app.
//
// PRECONDITION: FRESH DB. Run `e2e/reset-db.sh` (with the app stopped) then restart the app first.
// Run with:  e2e/run.sh e2e/flows/auth-smoke.js <port>
const page = await browser.getPage('auth-smoke')

// 1) First-run setup creates the root Admin and lands on the journal.
const creds = await completeSetup(page, { username: uniqueUsername('admin') })
console.log('setup OK as', creds.username)

// 2) Sign out path: a fresh login with the just-created identity also lands on the journal.
await gotoApp(page, '/login')
const after = await login(page, creds)
console.log('login OK as', after.username)

console.log('AUTH SMOKE: PASS')
