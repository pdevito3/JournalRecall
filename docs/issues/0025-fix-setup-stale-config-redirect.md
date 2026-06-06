# 0025 — Fix `/setup` stale-config redirect after root-Admin creation

**Phase:** 9 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0001

## What to build

After an operator creates the root Admin on a fresh instance, the app must sign them in and land them
on the authenticated home page (`/`) — not bounce them back to `/setup`.

The redirect guards already exist and are correct: the client guard redirects `/setup → /login` once a
User exists, and the server access gate redirects `/app/setup → /app/login` likewise. The bug is a
**stale cache**: the public auth-config query (`needsSetup`) is cached with a 30s stale time, so right
after setup the guard re-reads the pre-setup `needsSetup: true` and funnels the just-created operator
back to `/setup`.

The fix is surgical and root-cause only: when the setup mutation succeeds, **invalidate the cached
auth-config query** (centralized in the setup hook's success handler, mirroring how the login hook seeds
the current-session cache). The existing setup → login → navigate-home flow then re-reads
`needsSetup: false`, the guard passes, and the operator lands authenticated on `/`. **Do not add new
guards** — the existing client guard and server access gate stay as-is.

## Acceptance criteria

- [ ] After completing `/setup`, the operator is signed in and lands on `/` (no redirect back to `/setup`).
- [ ] The cached auth-config (`needsSetup`) is invalidated/refetched as part of setup success, so the
      client guard sees `needsSetup: false` on the post-setup navigation.
- [ ] No new redirect guards are introduced; the existing client guard and server access gate are
      unchanged.
- [ ] A functional test covers the full flow: fresh instance → complete setup → authenticated on `/`,
      not bounced to `/setup`.

## Blocked by

- None - can start immediately
